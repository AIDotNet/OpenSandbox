using System.Net;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using OpenSandbox.Sdk.Models;

namespace OpenSandbox.Sdk;

public sealed class OpenSandboxClient : IOpenSandboxClient
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly OpenSandboxClientOptions _options;

    public OpenSandboxClient(HttpClient httpClient, IOptions<OpenSandboxClientOptions> options)
        : this(httpClient, options.Value)
    {
    }

    public OpenSandboxClient(HttpClient httpClient, OpenSandboxClientOptions options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();

        _httpClient.BaseAddress = _options.GetApiBaseUri();
        _httpClient.Timeout = _options.Timeout;
        if (!_httpClient.DefaultRequestHeaders.Accept.Any(x => string.Equals(x.MediaType, "application/json", StringComparison.OrdinalIgnoreCase)))
        {
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }
    }

    public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, "ping", null);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        throw await CreateExceptionAsync("Ping", response, cancellationToken);
    }

    public Task<ListSandboxesResult> ListSandboxesAsync(ListSandboxesRequest? request = null, CancellationToken cancellationToken = default)
    {
        request ??= new ListSandboxesRequest();
        var query = BuildListQuery(request);
        return SendAsync<ListSandboxesResult>(HttpMethod.Get, query, null, "List sandboxes", cancellationToken);
    }

    public Task<CreatedSandbox> CreateSandboxAsync(CreateSandboxRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }
        if (string.IsNullOrWhiteSpace(request.Image?.Uri))
        {
            throw new InvalidOperationException("Image.Uri is required.");
        }

        var payload = new
        {
            image = request.Image,
            timeout = request.Timeout,
            neverExpires = request.NeverExpires,
            resourceLimits = request.ResourceLimits,
            env = request.Env,
            metadata = request.Metadata,
            entrypoint = request.Entrypoint,
            volumes = request.Volumes,
            extensions = request.Extensions,
            networkPolicy = request.NetworkPolicy ?? new SandboxNetworkPolicy { DefaultAction = "Allow" }
        };

        return SendAsync<CreatedSandbox>(HttpMethod.Post, "sandboxes", payload, "Create sandbox", cancellationToken);
    }

    public Task<Sandbox?> GetSandboxAsync(string sandboxId, CancellationToken cancellationToken = default)
    {
        EnsureSandboxId(sandboxId);
        return SendOptionalAsync<Sandbox>(HttpMethod.Get, $"sandboxes/{Uri.EscapeDataString(sandboxId)}", null, "Get sandbox", cancellationToken);
    }

    public Task<SandboxUsage?> GetSandboxUsageAsync(string sandboxId, CancellationToken cancellationToken = default)
    {
        EnsureSandboxId(sandboxId);
        return SendOptionalAsync<SandboxUsage>(HttpMethod.Get, $"sandboxes/{Uri.EscapeDataString(sandboxId)}/stats", null, "Get sandbox usage", cancellationToken);
    }

    public async Task<SandboxEndpoint?> GetSandboxEndpointAsync(string sandboxId, int port, bool useServerProxy = true, CancellationToken cancellationToken = default)
    {
        EnsureSandboxId(sandboxId);
        EnsurePort(port);
        var path = $"sandboxes/{Uri.EscapeDataString(sandboxId)}/endpoints/{port}?use_server_proxy={useServerProxy.ToString().ToLowerInvariant()}";
        var result = await SendOptionalAsync<SandboxEndpoint>(HttpMethod.Get, path, null, "Get sandbox endpoint", cancellationToken);
        if (result == null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(result.Url) && !string.IsNullOrWhiteSpace(result.Endpoint))
        {
            result.Url = NormalizeEndpointUrl(result.Endpoint);
        }

        return string.IsNullOrWhiteSpace(result.Url) && string.IsNullOrWhiteSpace(result.Endpoint)
            ? null
            : result;
    }

    public Task<CommandExecutionResult?> ExecuteCommandAsync(string sandboxId, string command, CancellationToken cancellationToken = default)
    {
        EnsureSandboxId(sandboxId);
        if (string.IsNullOrWhiteSpace(command))
        {
            throw new InvalidOperationException("Command is required.");
        }

        return SendOptionalAsync<CommandExecutionResult>(HttpMethod.Post, $"sandboxes/{Uri.EscapeDataString(sandboxId)}/exec", new { command }, "Execute command", cancellationToken);
    }

    public Task<SandboxLogsResult?> GetSandboxLogsAsync(string sandboxId, int tail = 200, CancellationToken cancellationToken = default)
    {
        EnsureSandboxId(sandboxId);
        if (tail <= 0)
        {
            throw new InvalidOperationException("Tail must be greater than zero.");
        }

        return SendOptionalAsync<SandboxLogsResult>(HttpMethod.Get, $"sandboxes/{Uri.EscapeDataString(sandboxId)}/logs?tail={tail}", null, "Get sandbox logs", cancellationToken);
    }

    public Task<SandboxFileListResult?> ListFilesAsync(string sandboxId, string path, CancellationToken cancellationToken = default)
    {
        EnsureSandboxId(sandboxId);
        if (string.IsNullOrWhiteSpace(path))
        {
            path = "/";
        }

        return SendOptionalAsync<SandboxFileListResult>(HttpMethod.Get, $"sandboxes/{Uri.EscapeDataString(sandboxId)}/files?path={Uri.EscapeDataString(path)}", null, "List files", cancellationToken);
    }

    public Task<SandboxFileReadResult?> ReadFileAsync(string sandboxId, string path, CancellationToken cancellationToken = default)
    {
        EnsureSandboxId(sandboxId);
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("Path is required.");
        }

        return SendOptionalAsync<SandboxFileReadResult>(HttpMethod.Get, $"sandboxes/{Uri.EscapeDataString(sandboxId)}/files/content?path={Uri.EscapeDataString(path)}", null, "Read file", cancellationToken);
    }

    public async Task<bool> WriteFileAsync(string sandboxId, WriteSandboxFileRequest request, CancellationToken cancellationToken = default)
    {
        EnsureSandboxId(sandboxId);
        if (request == null || string.IsNullOrWhiteSpace(request.Path))
        {
            throw new InvalidOperationException("Path is required.");
        }

        using var httpRequest = CreateRequest(HttpMethod.Post, $"sandboxes/{Uri.EscapeDataString(sandboxId)}/files/content", request);
        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw await CreateExceptionAsync("Write file", response, cancellationToken);
        }

        return true;
    }

    public async Task<bool> CreateDirectoryAsync(string sandboxId, CreateSandboxDirectoryRequest request, CancellationToken cancellationToken = default)
    {
        EnsureSandboxId(sandboxId);
        if (request == null || string.IsNullOrWhiteSpace(request.Path))
        {
            throw new InvalidOperationException("Path is required.");
        }

        using var httpRequest = CreateRequest(HttpMethod.Post, $"sandboxes/{Uri.EscapeDataString(sandboxId)}/directories", request);
        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw await CreateExceptionAsync("Create directory", response, cancellationToken);
        }

        return true;
    }

    public async Task<bool> DeletePathAsync(string sandboxId, string path, bool recursive = false, CancellationToken cancellationToken = default)
    {
        EnsureSandboxId(sandboxId);
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("Path is required.");
        }

        using var request = CreateRequest(HttpMethod.Delete, $"sandboxes/{Uri.EscapeDataString(sandboxId)}/files?path={Uri.EscapeDataString(path)}&recursive={recursive.ToString().ToLowerInvariant()}", null);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw await CreateExceptionAsync("Delete path", response, cancellationToken);
        }

        return true;
    }

    public Task<Sandbox?> PauseSandboxAsync(string sandboxId, CancellationToken cancellationToken = default)
    {
        EnsureSandboxId(sandboxId);
        return SendOptionalAsync<Sandbox>(HttpMethod.Post, $"sandboxes/{Uri.EscapeDataString(sandboxId)}/pause", null, "Pause sandbox", cancellationToken);
    }

    public Task<Sandbox?> ResumeSandboxAsync(string sandboxId, CancellationToken cancellationToken = default)
    {
        EnsureSandboxId(sandboxId);
        return SendOptionalAsync<Sandbox>(HttpMethod.Post, $"sandboxes/{Uri.EscapeDataString(sandboxId)}/resume", null, "Resume sandbox", cancellationToken);
    }

    public Task<RenewSandboxExpirationResult?> RenewSandboxExpirationAsync(string sandboxId, DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
    {
        EnsureSandboxId(sandboxId);
        return SendOptionalAsync<RenewSandboxExpirationResult>(HttpMethod.Post, $"sandboxes/{Uri.EscapeDataString(sandboxId)}/renew-expiration", new { expiresAt = expiresAt.ToString("O") }, "Renew sandbox expiration", cancellationToken);
    }

    public async Task<bool> DeleteSandboxAsync(string sandboxId, CancellationToken cancellationToken = default)
    {
        EnsureSandboxId(sandboxId);
        using var request = CreateRequest(HttpMethod.Delete, $"sandboxes/{Uri.EscapeDataString(sandboxId)}", null);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        throw await CreateExceptionAsync("Delete sandbox", response, cancellationToken);
    }

    public Uri BuildTerminalWebSocketUri(string sandboxId)
    {
        EnsureSandboxId(sandboxId);
        return BuildWebSocketUri($"sandboxes/{Uri.EscapeDataString(sandboxId)}/terminal/ws");
    }

    public Uri BuildLogsWebSocketUri(string sandboxId)
    {
        EnsureSandboxId(sandboxId);
        return BuildWebSocketUri($"sandboxes/{Uri.EscapeDataString(sandboxId)}/logs/ws");
    }

    public async Task<ClientWebSocket> ConnectTerminalAsync(string sandboxId, CancellationToken cancellationToken = default)
    {
        EnsureSandboxId(sandboxId);
        return await ConnectWebSocketAsync(BuildTerminalWebSocketUri(sandboxId), cancellationToken);
    }

    public async Task<ClientWebSocket> ConnectLogsStreamAsync(string sandboxId, CancellationToken cancellationToken = default)
    {
        EnsureSandboxId(sandboxId);
        return await ConnectWebSocketAsync(BuildLogsWebSocketUri(sandboxId), cancellationToken);
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? payload, string operation, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, path, payload);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateExceptionAsync(operation, response, cancellationToken);
        }

        return await DeserializeAsync<T>(response, operation, cancellationToken);
    }

    private async Task<ClientWebSocket> ConnectWebSocketAsync(Uri uri, CancellationToken cancellationToken)
    {
        var webSocket = new ClientWebSocket();
        webSocket.Options.KeepAliveInterval = _options.WebSocketKeepAliveInterval;
        ApplyWebSocketAuthentication(webSocket.Options);

        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(_options.TerminalConnectTimeout);
        await webSocket.ConnectAsync(uri, timeoutCancellation.Token);
        return webSocket;
    }

    private async Task<T?> SendOptionalAsync<T>(HttpMethod method, string path, object? payload, string operation, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, path, payload);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw await CreateExceptionAsync(operation, response, cancellationToken);
        }

        return await DeserializeAsync<T>(response, operation, cancellationToken);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, object? payload)
    {
        var request = new HttpRequestMessage(method, path);
        ApplyHttpAuthentication(request.Headers);
        if (payload != null)
        {
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        return request;
    }

    private void ApplyHttpAuthentication(HttpRequestHeaders headers)
    {
        headers.Remove("OPEN-SANDBOX-API-KEY");
        headers.Remove("OPEN_SANDBOX_API_KEY");
        headers.Authorization = null;

        if (_options.AuthenticationMode == OpenSandboxAuthenticationMode.ApiKey)
        {
            headers.TryAddWithoutValidation("OPEN-SANDBOX-API-KEY", _options.ApiKey);
        }
        else
        {
            headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.BearerToken);
        }
    }

    private void ApplyWebSocketAuthentication(ClientWebSocketOptions options)
    {
        if (_options.AuthenticationMode == OpenSandboxAuthenticationMode.ApiKey)
        {
            options.SetRequestHeader("OPEN-SANDBOX-API-KEY", _options.ApiKey);
        }
        else
        {
            options.SetRequestHeader("Authorization", $"Bearer {_options.BearerToken}");
        }
    }

    private static async Task<T> DeserializeAsync<T>(HttpResponseMessage response, string operation, CancellationToken cancellationToken)
    {
        var body = response.Content == null ? null : await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new OpenSandboxException($"{operation} returned an empty response.", response.StatusCode);
        }

        var result = JsonSerializer.Deserialize<T>(body, JsonOptions);
        return result ?? throw new OpenSandboxException($"{operation} returned an invalid response body.", response.StatusCode, responseBody: body);
    }

    private static async Task<OpenSandboxException> CreateExceptionAsync(string operation, HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = response.Content == null ? null : await response.Content.ReadAsStringAsync();
        return OpenSandboxException.FromResponse(operation, response.StatusCode, body);
    }

    private static void EnsureSandboxId(string sandboxId)
    {
        if (string.IsNullOrWhiteSpace(sandboxId))
        {
            throw new InvalidOperationException("Sandbox id is required.");
        }
    }

    private static void EnsurePort(int port)
    {
        if (port <= 0)
        {
            throw new InvalidOperationException("Port must be greater than zero.");
        }
    }

    private string BuildListQuery(ListSandboxesRequest request)
    {
        var pairs = new List<string>
        {
            $"page={request.Page}",
            $"pageSize={request.PageSize}"
        };

        if (request.States != null && request.States.Count > 0)
        {
            pairs.Add($"states={Uri.EscapeDataString(string.Join(',', request.States))}");
        }

        if (request.Metadata != null)
        {
            foreach (var item in request.Metadata)
            {
                pairs.Add($"metadata.{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(item.Value)}");
            }
        }

        return "sandboxes?" + string.Join("&", pairs);
    }

    private string? NormalizeEndpointUrl(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return null;
        }

        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri.ToString();
        }

        var serverUri = new Uri(_options.BaseUrl.Trim().TrimEnd('/'));
        if (endpoint.StartsWith('/'))
        {
            return $"{serverUri.Scheme}://{serverUri.Authority}{endpoint}";
        }

        return $"{serverUri.Scheme}://{endpoint}";
    }

    private Uri BuildWebSocketUri(string relativePath)
    {
        var targetUri = new Uri(_options.GetApiBaseUri(), relativePath);
        var builder = new UriBuilder(targetUri)
        {
            Scheme = string.Equals(targetUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ? "wss" : "ws"
        };
        return builder.Uri;
    }
}
