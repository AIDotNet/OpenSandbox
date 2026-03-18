using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using OpenSandbox.Abstractions.Services;
using OpenSandbox.Runtime.Docker;
using OpenSandbox.Runtime.Docker.Options;
using OpenSandbox.Server.Contracts;
using OpenSandbox.Server.Options;
using OpenSandbox.Server.Proxy;
using OpenSandbox.Server.Services;
using OpenSandbox.Store.FileSystem;
using OpenSandbox.Store.FileSystem.Options;
using Yarp.ReverseProxy.Forwarder;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<OpenSandboxServerOptions>(builder.Configuration.GetSection(OpenSandboxServerOptions.SectionName));
builder.Services.Configure<DockerRuntimeOptions>(builder.Configuration.GetSection(DockerRuntimeOptions.SectionName));
builder.Services.Configure<FileSystemStoreOptions>(builder.Configuration.GetSection(FileSystemStoreOptions.SectionName));
builder.Services.AddSingleton<ISandboxStore, FileSystemSandboxStore>();
builder.Services.AddSingleton<ISandboxRuntime, DockerSandboxRuntime>();
builder.Services.AddSingleton<SignedProxyUrlService>();
builder.Services.AddSingleton<OpenSandboxService>();
builder.Services.AddHostedService<SandboxExpirationBackgroundService>();
builder.Services.AddHealthChecks();
builder.Services.AddHttpForwarder();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "OpenSandbox Server",
        Version = "v1"
    });
});
builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<IOptions<OpenSandboxServerOptions>>().Value;
    return new ForwarderRequestConfig
    {
        ActivityTimeout = TimeSpan.FromMinutes(Math.Max(1, options.Proxy.ActivityTimeoutMinutes)),
        AllowResponseBuffering = false
    };
});
builder.Services.AddSingleton<HttpMessageInvoker>(_ =>
{
    var handler = new SocketsHttpHandler
    {
        UseProxy = false,
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.None,
        UseCookies = false,
        EnableMultipleHttp2Connections = true
    };
    return new HttpMessageInvoker(handler, disposeHandler: true);
});

var app = builder.Build();

app.UseWebSockets();
app.UseSwagger(c =>
{
    c.RouteTemplate = "openapi/{documentName}.json";
});
app.MapHealthChecks("/health", new HealthCheckOptions());

app.Use(async (context, next) =>
{
    if (!(context.Request.Path.Value?.StartsWith("/v1", StringComparison.OrdinalIgnoreCase) ?? false))
    {
        await next();
        return;
    }

    var options = context.RequestServices.GetRequiredService<IOptions<OpenSandboxServerOptions>>().Value;
    if (context.RequestServices.GetRequiredService<SignedProxyUrlService>().TryAuthorize(context))
    {
        await next();
        return;
    }

    if (options.Tokens.Count == 0)
    {
        await next();
        return;
    }

    if (TryReadApiKey(context, options.Tokens, out var apiKey))
    {
        context.Items["OpenSandbox.AuthHeaderName"] = "OPEN-SANDBOX-API-KEY";
        context.Items["OpenSandbox.AuthHeaderValue"] = apiKey;
        await next();
        return;
    }

    if (TryReadBearer(context, options.Tokens, out var bearerHeader))
    {
        context.Items["OpenSandbox.AuthHeaderName"] = "Authorization";
        context.Items["OpenSandbox.AuthHeaderValue"] = bearerHeader;
        await next();
        return;
    }

    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
    await context.Response.WriteAsJsonAsync(new
    {
        error = new
        {
            code = "Unauthorized",
            message = "Missing or invalid API key or bearer token."
        }
    });
});

var v1 = app.MapGroup("/v1");

v1.MapGet("/ping", () => Results.Ok(new PingResponse()));

v1.MapGet("/sandboxes", async (HttpContext httpContext, OpenSandboxService sandboxService, CancellationToken cancellationToken) =>
{
    var page = ParseInt(httpContext.Request.Query["page"].ToString(), 1);
    var pageSize = ParseInt(httpContext.Request.Query["pageSize"].ToString(), 20);
    var states = ParseStates(httpContext.Request.Query["states"]);
    var metadata = ParseMetadataFilters(httpContext.Request.Query);
    var result = await sandboxService.ListAsync(page, pageSize, states, metadata, cancellationToken);
    return Results.Ok(result);
});

v1.MapPost("/sandboxes", async (CreateSandboxRequest request, OpenSandboxService sandboxService, CancellationToken cancellationToken) =>
{
    var result = await sandboxService.CreateAsync(request, cancellationToken);
    return Results.Ok(result);
});

v1.MapGet("/sandboxes/{id}", async (string id, OpenSandboxService sandboxService, CancellationToken cancellationToken) =>
{
    var result = await sandboxService.GetAsync(id, cancellationToken);
    return result == null ? Results.NotFound() : Results.Ok(result);
});

v1.MapGet("/sandboxes/{id}/stats", async (string id, OpenSandboxService sandboxService, CancellationToken cancellationToken) =>
{
    var result = await sandboxService.GetUsageAsync(id, cancellationToken);
    return result == null ? Results.NotFound() : Results.Ok(result);
});

v1.MapGet("/sandboxes/{id}/logs", async (string id, HttpContext httpContext, OpenSandboxService sandboxService, CancellationToken cancellationToken) =>
{
    var tail = ParseInt(httpContext.Request.Query["tail"].ToString(), 200);
    var result = await sandboxService.GetLogsAsync(id, tail, cancellationToken);
    return result == null ? Results.NotFound() : Results.Ok(result);
});

v1.MapPost("/sandboxes/{id}/exec", async (string id, ExecuteCommandRequest request, OpenSandboxService sandboxService, CancellationToken cancellationToken) =>
{
    var result = await sandboxService.ExecuteCommandAsync(id, request.Command, cancellationToken);
    return result == null ? Results.NotFound() : Results.Ok(result);
});

v1.MapGet("/sandboxes/{id}/files", async (string id, HttpContext httpContext, OpenSandboxService sandboxService, CancellationToken cancellationToken) =>
{
    var path = httpContext.Request.Query["path"].ToString();
    var result = await sandboxService.ListFilesAsync(id, path, cancellationToken);
    return result == null ? Results.NotFound() : Results.Ok(result);
});

v1.MapGet("/sandboxes/{id}/files/content", async (string id, HttpContext httpContext, OpenSandboxService sandboxService, CancellationToken cancellationToken) =>
{
    var path = httpContext.Request.Query["path"].ToString();
    if (string.IsNullOrWhiteSpace(path))
    {
        return Results.BadRequest(new { error = new { code = "InvalidArgument", message = "path is required." } });
    }

    var result = await sandboxService.ReadFileAsync(id, path, cancellationToken);
    return result == null ? Results.NotFound() : Results.Ok(result);
});

v1.MapPost("/sandboxes/{id}/files/content", async (string id, WriteFileRequest request, OpenSandboxService sandboxService, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Path))
    {
        return Results.BadRequest(new { error = new { code = "InvalidArgument", message = "path is required." } });
    }

    var ok = await sandboxService.WriteFileAsync(id, request, cancellationToken);
    return ok ? Results.Ok(new { success = true }) : Results.NotFound();
});

v1.MapPost("/sandboxes/{id}/directories", async (string id, CreateDirectoryRequest request, OpenSandboxService sandboxService, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Path))
    {
        return Results.BadRequest(new { error = new { code = "InvalidArgument", message = "path is required." } });
    }

    var ok = await sandboxService.CreateDirectoryAsync(id, request, cancellationToken);
    return ok ? Results.Ok(new { success = true }) : Results.NotFound();
});

v1.MapDelete("/sandboxes/{id}/files", async (string id, HttpContext httpContext, OpenSandboxService sandboxService, CancellationToken cancellationToken) =>
{
    var path = httpContext.Request.Query["path"].ToString();
    if (string.IsNullOrWhiteSpace(path))
    {
        return Results.BadRequest(new { error = new { code = "InvalidArgument", message = "path is required." } });
    }

    var recursive = bool.TryParse(httpContext.Request.Query["recursive"].ToString(), out var parsed) && parsed;
    var ok = await sandboxService.DeletePathAsync(id, path, recursive, cancellationToken);
    return ok ? Results.NoContent() : Results.NotFound();
});

app.Map("/v1/sandboxes/{id}/terminal/ws", TerminalWebSocketAsync);
app.Map("/v1/sandboxes/{id}/logs/ws", LogsWebSocketAsync);

v1.MapDelete("/sandboxes/{id}", async (string id, OpenSandboxService sandboxService, CancellationToken cancellationToken) =>
{
    var deleted = await sandboxService.DeleteAsync(id, cancellationToken);
    return deleted ? Results.NoContent() : Results.NotFound();
});

v1.MapPost("/sandboxes/{id}/pause", PauseAsync);
v1.MapPost("/sandboxes/{id}:pause", PauseAsync);
v1.MapPost("/sandboxes/{id}/resume", ResumeAsync);
v1.MapPost("/sandboxes/{id}:resume", ResumeAsync);
v1.MapPost("/sandboxes/{id}/restart", RestartAsync);
v1.MapPost("/sandboxes/{id}:restart", RestartAsync);
v1.MapPost("/sandboxes/{id}/renew", RenewAsync);
v1.MapPost("/sandboxes/{id}:renew", RenewAsync);
v1.MapPost("/sandboxes/{id}/renew-expiration", RenewAsync);
v1.MapPost("/sandboxes/{id}:renew-expiration", RenewAsync);
v1.MapGet("/sandboxes/{id}/endpoints/{port:int}", GetEndpointAsync);
v1.MapGet("/sandboxes/{id}/endpoint/{port:int}", GetEndpointAsync);
v1.MapGet("/sandboxes/{id}/endpoint", GetEndpointByQueryAsync);
v1.MapGet("/sandboxes/{id}/endpoints", GetEndpointByQueryAsync);

app.Map("/v1/sandboxes/{id}/proxy/{port:int}", ForwardProxyAsync);
app.Map("/v1/sandboxes/{id}/proxy/{port:int}/{**catchall}", ForwardProxyAsync);

app.Run();
return;

static async Task<IResult> PauseAsync(string id, OpenSandboxService sandboxService, CancellationToken cancellationToken)
{
    var result = await sandboxService.PauseAsync(id, cancellationToken);
    return result == null ? Results.NotFound() : Results.Ok(result);
}

static async Task<IResult> ResumeAsync(string id, OpenSandboxService sandboxService, CancellationToken cancellationToken)
{
    var result = await sandboxService.ResumeAsync(id, cancellationToken);
    return result == null ? Results.NotFound() : Results.Ok(result);
}

static async Task<IResult> RestartAsync(string id, OpenSandboxService sandboxService, CancellationToken cancellationToken)
{
    var result = await sandboxService.RestartAsync(id, cancellationToken);
    return result == null ? Results.NotFound() : Results.Ok(result);
}

static async Task<IResult> RenewAsync(string id, RenewSandboxExpirationRequest request, OpenSandboxService sandboxService, CancellationToken cancellationToken)
{
    if (!DateTimeOffset.TryParse(request.ExpiresAt, out var expiresAt))
    {
        return Results.BadRequest(new { error = new { code = "InvalidArgument", message = "expiresAt is invalid." } });
    }

    var result = await sandboxService.RenewAsync(id, expiresAt, cancellationToken);
    return result == null ? Results.NotFound() : Results.Ok(result);
}

static async Task<IResult> TerminalWebSocketAsync(string id, HttpContext httpContext, OpenSandboxService sandboxService, ISandboxRuntime runtime, CancellationToken cancellationToken)
{
    if (!httpContext.WebSockets.IsWebSocketRequest)
    {
        return Results.BadRequest(new { error = new { code = "InvalidArgument", message = "WebSocket request is required." } });
    }

    var containerName = await sandboxService.GetTerminalContainerNameAsync(id, cancellationToken);
    if (string.IsNullOrWhiteSpace(containerName))
    {
        return Results.NotFound();
    }

    using var webSocket = await httpContext.WebSockets.AcceptWebSocketAsync();
    await runtime.RunTerminalSessionAsync(containerName, webSocket, cancellationToken);
    return Results.Empty;
}

static async Task<IResult> LogsWebSocketAsync(string id, HttpContext httpContext, OpenSandboxService sandboxService, ISandboxRuntime runtime, CancellationToken cancellationToken)
{
    if (!httpContext.WebSockets.IsWebSocketRequest)
    {
        return Results.BadRequest(new { error = new { code = "InvalidArgument", message = "WebSocket request is required." } });
    }

    var containerName = await sandboxService.GetLogsContainerNameAsync(id, cancellationToken);
    if (string.IsNullOrWhiteSpace(containerName))
    {
        return Results.NotFound();
    }

    using var webSocket = await httpContext.WebSockets.AcceptWebSocketAsync();
    await runtime.StreamLogsAsync(containerName, webSocket, cancellationToken);
    return Results.Empty;
}

static async Task<IResult> GetEndpointAsync(string id, int port, HttpContext httpContext, OpenSandboxService sandboxService, CancellationToken cancellationToken)
{
    var useServerProxy = GetUseServerProxy(httpContext.Request.Query);
    var result = await sandboxService.GetEndpointAsync(id, port, useServerProxy, httpContext, cancellationToken);
    return result == null ? Results.NotFound() : Results.Ok(result);
}

static async Task<IResult> GetEndpointByQueryAsync(string id, HttpContext httpContext, OpenSandboxService sandboxService, CancellationToken cancellationToken)
{
    var port = ParseInt(httpContext.Request.Query["port"].ToString(), 0);
    if (port <= 0)
    {
        return Results.BadRequest(new { error = new { code = "InvalidArgument", message = "port must be greater than 0." } });
    }

    var useServerProxy = GetUseServerProxy(httpContext.Request.Query);
    var result = await sandboxService.GetEndpointAsync(id, port, useServerProxy, httpContext, cancellationToken);
    return result == null ? Results.NotFound() : Results.Ok(result);
}

static async Task<IResult> ForwardProxyAsync(HttpContext httpContext, string id, int port, string? catchall, OpenSandboxService sandboxService, IHttpForwarder forwarder, HttpMessageInvoker httpClient, ForwarderRequestConfig forwarderRequestConfig, CancellationToken cancellationToken)
{
    var destinationPrefix = await sandboxService.ResolveProxyDestinationAsync(id, port, cancellationToken);
    if (string.IsNullOrWhiteSpace(destinationPrefix))
    {
        return Results.NotFound();
    }

    var path = catchall ?? string.Empty;
    var isWebSocketUpgrade = string.Equals(httpContext.Request.Headers.Upgrade, "websocket", StringComparison.OrdinalIgnoreCase);
    if (string.IsNullOrWhiteSpace(path)
        && (HttpMethods.IsGet(httpContext.Request.Method) || HttpMethods.IsHead(httpContext.Request.Method))
        && !isWebSocketUpgrade
        && !(httpContext.Request.Path.Value?.EndsWith('/') ?? false))
    {
        var redirectUrl = $"{httpContext.Request.PathBase}{httpContext.Request.Path}/{httpContext.Request.QueryString}";
        return Results.Redirect(redirectUrl);
    }

    if (!httpContext.Request.Headers.ContainsKey("X-Forwarded-Proto"))
    {
        httpContext.Request.Headers.Append("X-Forwarded-Proto", httpContext.Request.Scheme);
    }

    if (!httpContext.Request.Headers.ContainsKey("X-Forwarded-Host") && httpContext.Request.Host.HasValue)
    {
        httpContext.Request.Headers.Append("X-Forwarded-Host", httpContext.Request.Host.Value);
    }

    if (!httpContext.Request.Headers.ContainsKey("X-Forwarded-For") && httpContext.Connection.RemoteIpAddress != null)
    {
        httpContext.Request.Headers.Append("X-Forwarded-For", httpContext.Connection.RemoteIpAddress.ToString());
    }

    httpContext.Request.Path = string.IsNullOrWhiteSpace(path) ? new PathString("/") : new PathString("/" + path);
    var error = await forwarder.SendAsync(httpContext, destinationPrefix, httpClient, forwarderRequestConfig, new SandboxProxyTransformer(id, port), cancellationToken);

    return error == ForwarderError.None
        ? Results.Empty
        : Results.Problem("Sandbox proxy failed.", statusCode: StatusCodes.Status502BadGateway);
}

static bool TryReadApiKey(HttpContext context, IReadOnlyCollection<string> tokens, out string apiKey)
{
    apiKey = string.Empty;
    if (!context.Request.Headers.TryGetValue("OPEN-SANDBOX-API-KEY", out var headerValue)
        && !context.Request.Headers.TryGetValue("OPEN_SANDBOX_API_KEY", out headerValue))
    {
        return false;
    }

    var value = headerValue.ToString();
    if (tokens.Any(x => string.Equals(x, value, StringComparison.Ordinal)))
    {
        apiKey = value;
        return true;
    }

    return false;
}

static bool TryReadBearer(HttpContext context, IReadOnlyCollection<string> tokens, out string bearerHeader)
{
    bearerHeader = string.Empty;
    var authorization = context.Request.Headers.Authorization.ToString();
    if (string.IsNullOrWhiteSpace(authorization))
    {
        return false;
    }

    if (!AuthenticationHeaderValue.TryParse(authorization, out var header)
        || !string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
        || string.IsNullOrWhiteSpace(header.Parameter))
    {
        return false;
    }

    if (tokens.Any(x => string.Equals(x, header.Parameter, StringComparison.Ordinal)))
    {
        bearerHeader = $"Bearer {header.Parameter}";
        return true;
    }

    return false;
}

static int ParseInt(string? value, int defaultValue)
{
    return int.TryParse(value, out var result) ? result : defaultValue;
}

static IReadOnlyCollection<string> ParseStates(Microsoft.Extensions.Primitives.StringValues values)
{
    return values
        .SelectMany(x => (x ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}

static IReadOnlyDictionary<string, string> ParseMetadataFilters(IQueryCollection query)
{
    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var item in query)
    {
        if (item.Key.StartsWith("metadata.", StringComparison.OrdinalIgnoreCase))
        {
            result[item.Key[9..]] = item.Value.ToString();
            continue;
        }

        if (item.Key.StartsWith("metadata[", StringComparison.OrdinalIgnoreCase) && item.Key.EndsWith(']'))
        {
            var key = item.Key[9..^1];
            result[key] = item.Value.ToString();
            continue;
        }

        if (string.Equals(item.Key, "metadata", StringComparison.OrdinalIgnoreCase))
        {
            var raw = item.Value.ToString();
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            try
            {
                var parsed = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(raw);
                if (parsed != null)
                {
                    foreach (var pair in parsed)
                    {
                        result[pair.Key] = pair.Value;
                    }
                }
            }
            catch
            {
                foreach (var pair in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var parts = pair.Split('=', 2, StringSplitOptions.TrimEntries);
                    if (parts.Length == 2)
                    {
                        result[parts[0]] = parts[1];
                    }
                }
            }
        }
    }

    return result;
}

static bool GetUseServerProxy(IQueryCollection query)
{
    foreach (var key in new[] { "useServerProxy", "serverProxy", "use-server-proxy", "use_server_proxy" })
    {
        if (bool.TryParse(query[key].ToString(), out var value))
        {
            return value;
        }
    }

    return false;
}
