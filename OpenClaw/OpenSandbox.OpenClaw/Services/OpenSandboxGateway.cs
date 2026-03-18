using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using OpenSandbox.Server.Contracts;

namespace OpenSandbox.OpenClaw.Services;

public sealed class OpenSandboxGateway(IHttpClientFactory httpClientFactory)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<bool> PingAsync(string baseUrl, string token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Combine(baseUrl, "/v1/ping"));
        request.Headers.Add("OPEN-SANDBOX-API-KEY", token);
        using var client = httpClientFactory.CreateClient(nameof(OpenSandboxGateway));
        using var response = await client.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<CreateSandboxResponse> CreateSandboxAsync(string baseUrl, string token, CreateSandboxRequest requestBody, CancellationToken cancellationToken)
    {
        using var request = CreateJsonRequest(HttpMethod.Post, Combine(baseUrl, "/v1/sandboxes"), token, requestBody);
        using var client = httpClientFactory.CreateClient(nameof(OpenSandboxGateway));
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreateSandboxResponse>(JsonOptions, cancellationToken))!;
    }

    public async Task<SandboxInfoResponse?> GetSandboxAsync(string baseUrl, string token, string sandboxId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Combine(baseUrl, $"/v1/sandboxes/{sandboxId}"));
        request.Headers.Add("OPEN-SANDBOX-API-KEY", token);
        using var client = httpClientFactory.CreateClient(nameof(OpenSandboxGateway));
        using var response = await client.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SandboxInfoResponse>(JsonOptions, cancellationToken);
    }

    public async Task<SandboxUsageResponse?> GetStatsAsync(string baseUrl, string token, string sandboxId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Combine(baseUrl, $"/v1/sandboxes/{sandboxId}/stats"));
        request.Headers.Add("OPEN-SANDBOX-API-KEY", token);
        using var client = httpClientFactory.CreateClient(nameof(OpenSandboxGateway));
        using var response = await client.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SandboxUsageResponse>(JsonOptions, cancellationToken);
    }

    public async Task<SandboxLogsResponse?> GetLogsAsync(string baseUrl, string token, string sandboxId, int tail, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Combine(baseUrl, $"/v1/sandboxes/{sandboxId}/logs?tail={tail}"));
        request.Headers.Add("OPEN-SANDBOX-API-KEY", token);
        using var client = httpClientFactory.CreateClient(nameof(OpenSandboxGateway));
        using var response = await client.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SandboxLogsResponse>(JsonOptions, cancellationToken);
    }

    public async Task<ListFilesResponse?> ListFilesAsync(string baseUrl, string token, string sandboxId, string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Combine(baseUrl, $"/v1/sandboxes/{sandboxId}/files?path={Uri.EscapeDataString(string.IsNullOrWhiteSpace(path) ? "/" : path)}"));
        request.Headers.Add("OPEN-SANDBOX-API-KEY", token);
        using var client = httpClientFactory.CreateClient(nameof(OpenSandboxGateway));
        using var response = await client.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, $"List files for sandbox '{sandboxId}'", cancellationToken);
        return await response.Content.ReadFromJsonAsync<ListFilesResponse>(JsonOptions, cancellationToken);
    }

    public async Task<ReadFileResponse?> ReadFileAsync(string baseUrl, string token, string sandboxId, string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Combine(baseUrl, $"/v1/sandboxes/{sandboxId}/files/content?path={Uri.EscapeDataString(path)}"));
        request.Headers.Add("OPEN-SANDBOX-API-KEY", token);
        using var client = httpClientFactory.CreateClient(nameof(OpenSandboxGateway));
        using var response = await client.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, $"Read file '{path}' for sandbox '{sandboxId}'", cancellationToken);
        return await response.Content.ReadFromJsonAsync<ReadFileResponse>(JsonOptions, cancellationToken);
    }

    public async Task<bool> WriteFileAsync(string baseUrl, string token, string sandboxId, WriteFileRequest body, CancellationToken cancellationToken)
    {
        using var request = CreateJsonRequest(HttpMethod.Post, Combine(baseUrl, $"/v1/sandboxes/{sandboxId}/files/content"), token, body);
        using var client = httpClientFactory.CreateClient(nameof(OpenSandboxGateway));
        using var response = await client.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }

        await EnsureSuccessAsync(response, $"Write file '{body.Path}' for sandbox '{sandboxId}'", cancellationToken);
        return true;
    }

    public async Task<bool> CreateDirectoryAsync(string baseUrl, string token, string sandboxId, CreateDirectoryRequest body, CancellationToken cancellationToken)
    {
        using var request = CreateJsonRequest(HttpMethod.Post, Combine(baseUrl, $"/v1/sandboxes/{sandboxId}/directories"), token, body);
        using var client = httpClientFactory.CreateClient(nameof(OpenSandboxGateway));
        using var response = await client.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }

        await EnsureSuccessAsync(response, $"Create directory '{body.Path}' for sandbox '{sandboxId}'", cancellationToken);
        return true;
    }

    public async Task<bool> DeletePathAsync(string baseUrl, string token, string sandboxId, string path, bool recursive, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, Combine(baseUrl, $"/v1/sandboxes/{sandboxId}/files?path={Uri.EscapeDataString(path)}&recursive={recursive.ToString().ToLowerInvariant()}"));
        request.Headers.Add("OPEN-SANDBOX-API-KEY", token);
        using var client = httpClientFactory.CreateClient(nameof(OpenSandboxGateway));
        using var response = await client.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }

        await EnsureSuccessAsync(response, $"Delete path '{path}' for sandbox '{sandboxId}'", cancellationToken);
        return true;
    }

    public async Task<SandboxInfoResponse?> RestartSandboxAsync(string baseUrl, string token, string sandboxId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Combine(baseUrl, $"/v1/sandboxes/{sandboxId}/restart"));
        request.Headers.Add("OPEN-SANDBOX-API-KEY", token);
        using var client = httpClientFactory.CreateClient(nameof(OpenSandboxGateway));
        using var response = await client.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SandboxInfoResponse>(JsonOptions, cancellationToken);
    }

    public async Task DeleteSandboxAsync(string baseUrl, string token, string sandboxId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, Combine(baseUrl, $"/v1/sandboxes/{sandboxId}"));
        request.Headers.Add("OPEN-SANDBOX-API-KEY", token);
        using var client = httpClientFactory.CreateClient(nameof(OpenSandboxGateway));
        using var response = await client.SendAsync(request, cancellationToken);
        if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            response.EnsureSuccessStatusCode();
        }
    }

    public async Task<ClientWebSocket> ConnectWebSocketAsync(Uri remoteUri, string token, CancellationToken cancellationToken)
    {
        var upstream = new ClientWebSocket();
        upstream.Options.SetRequestHeader("OPEN-SANDBOX-API-KEY", token);

        try
        {
            await upstream.ConnectAsync(remoteUri, cancellationToken);
            return upstream;
        }
        catch
        {
            upstream.Dispose();
            throw;
        }
    }

    public async Task BridgeWebSocketAsync(WebSocket upstream, WebSocket clientSocket, CancellationToken cancellationToken)
    {
        var forward = PumpAsync(clientSocket, upstream, cancellationToken);
        var backward = PumpAsync(upstream, clientSocket, cancellationToken);
        await Task.WhenAny(forward, backward);

        await CloseSilentlyAsync(upstream);
        await CloseSilentlyAsync(clientSocket);
    }

    private static async Task PumpAsync(WebSocket source, WebSocket destination, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        while (!cancellationToken.IsCancellationRequested && source.State == WebSocketState.Open && destination.State == WebSocketState.Open)
        {
            var result = await source.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                break;
            }

            await destination.SendAsync(new ArraySegment<byte>(buffer, 0, result.Count), result.MessageType, result.EndOfMessage, cancellationToken);
        }
    }

    private static async Task CloseSilentlyAsync(WebSocket socket)
    {
        if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            try
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "closed", CancellationToken.None);
            }
            catch
            {
            }
        }
    }

    private static HttpRequestMessage CreateJsonRequest(HttpMethod method, string url, string token, object body)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("OPEN-SANDBOX-API-KEY", token);
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        return request;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string action, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = response.Content == null ? string.Empty : await response.Content.ReadAsStringAsync(cancellationToken);
        var detail = string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase ?? "Unknown error" : body;
        throw new InvalidOperationException($"{action} failed: {(int)response.StatusCode} {detail}");
    }

    private static string Combine(string baseUrl, string path) => $"{baseUrl.TrimEnd('/')}{path}";
}
