using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using OpenSandbox.Sdk.Models;

namespace OpenSandbox.Sdk.Tests;

public sealed class OpenSandboxClientTests
{
    [Fact]
    public async Task ApiKeyClient_CanCall_AllCoreEndpoints()
    {
        await using var app = await CreateServerAsync(OpenSandboxAuthenticationMode.ApiKey);
        var client = new OpenSandboxClient(app.GetTestClient(), new OpenSandboxClientOptions
        {
            BaseUrl = "http://localhost",
            AuthenticationMode = OpenSandboxAuthenticationMode.ApiKey,
            ApiKey = "test-key"
        });

        var ping = await client.PingAsync();
        var list = await client.ListSandboxesAsync(new ListSandboxesRequest
        {
            Page = 1,
            PageSize = 10,
            States = new[] { "Running" },
            Metadata = new Dictionary<string, string> { ["tenant"] = "demo" }
        });
        var created = await client.CreateSandboxAsync(new CreateSandboxRequest
        {
            Image = new SandboxImageReference { Uri = "ghcr.io/demo/image:latest" },
            Timeout = 600,
            NeverExpires = true,
            Entrypoint = new List<string> { "bash" },
            ResourceLimits = new SandboxResourceLimits { Cpu = "500m", Memory = "512Mi" },
            Metadata = new Dictionary<string, string> { ["tenant"] = "demo" }
        });
        var sandbox = await client.GetSandboxAsync("sb-1");
        var usage = await client.GetSandboxUsageAsync("sb-1");
        var endpoint = await client.GetSandboxEndpointAsync("sb-1", 8080);
        var exec = await client.ExecuteCommandAsync("sb-1", "echo hi");
        var logs = await client.GetSandboxLogsAsync("sb-1", 50);
        var paused = await client.PauseSandboxAsync("sb-1");
        var resumed = await client.ResumeSandboxAsync("sb-1");
        var renewed = await client.RenewSandboxExpirationAsync("sb-1", DateTimeOffset.UtcNow.AddMinutes(30));
        var deleted = await client.DeleteSandboxAsync("sb-1");
        var terminalUri = client.BuildTerminalWebSocketUri("sb-1");
        var logsUri = client.BuildLogsWebSocketUri("sb-1");

        Assert.True(ping);
        Assert.Single(list.Items);
        Assert.Equal("sb-created", created.Id);
        Assert.True(created.NeverExpires);
        Assert.Equal("sb-1", sandbox?.Id);
        Assert.Equal(12.5m, usage?.CpuPercent);
        Assert.Equal("https://demo.local/sb-1", endpoint?.Url);
        Assert.Equal(0, exec?.ExitCode);
        Assert.Equal(2, logs?.Lines.Count);
        Assert.Equal("Paused", paused?.Status?.State);
        Assert.Equal("Running", resumed?.Status?.State);
        Assert.NotNull(renewed?.ExpiresAt);
        Assert.True(deleted);
        Assert.Equal("ws", terminalUri.Scheme);
        Assert.Equal("ws", logsUri.Scheme);
        Assert.Contains("/v1/sandboxes/sb-1/terminal/ws", terminalUri.AbsoluteUri, StringComparison.Ordinal);
        Assert.Contains("/v1/sandboxes/sb-1/logs/ws", logsUri.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BearerClient_SendsAuthorizationHeader()
    {
        await using var app = await CreateServerAsync(OpenSandboxAuthenticationMode.Bearer);
        var client = new OpenSandboxClient(app.GetTestClient(), new OpenSandboxClientOptions
        {
            BaseUrl = "http://localhost",
            AuthenticationMode = OpenSandboxAuthenticationMode.Bearer,
            BearerToken = "test-token"
        });

        var ping = await client.PingAsync();

        Assert.True(ping);
    }

    [Fact]
    public async Task EndpointAndTerminalMethods_ValidateArguments()
    {
        await using var app = await CreateServerAsync(OpenSandboxAuthenticationMode.ApiKey);
        var client = new OpenSandboxClient(app.GetTestClient(), new OpenSandboxClientOptions
        {
            BaseUrl = "http://localhost",
            AuthenticationMode = OpenSandboxAuthenticationMode.ApiKey,
            ApiKey = "test-key"
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetSandboxEndpointAsync("sb-1", 0));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetSandboxLogsAsync("sb-1", 0));
        Assert.Throws<InvalidOperationException>(() => client.BuildTerminalWebSocketUri(string.Empty));
        Assert.Throws<InvalidOperationException>(() => client.BuildLogsWebSocketUri(string.Empty));
    }

    [Fact]
    public void Options_ValidateTerminalSettings()
    {
        var options = new OpenSandboxClientOptions
        {
            BaseUrl = "http://localhost",
            AuthenticationMode = OpenSandboxAuthenticationMode.ApiKey,
            ApiKey = "test-key",
            WebSocketKeepAliveInterval = TimeSpan.FromSeconds(-1)
        };

        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void Options_AllowInfiniteRequestTimeout()
    {
        var options = new OpenSandboxClientOptions
        {
            BaseUrl = "http://localhost",
            AuthenticationMode = OpenSandboxAuthenticationMode.ApiKey,
            ApiKey = "test-key",
            Timeout = System.Threading.Timeout.InfiniteTimeSpan
        };

        options.Validate();
        Assert.Equal(System.Threading.Timeout.InfiniteTimeSpan, options.Timeout);
    }

    private static async Task<WebApplication> CreateServerAsync(OpenSandboxAuthenticationMode mode)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var app = builder.Build();

        app.Use(async (context, next) =>
        {
            var authorized = mode == OpenSandboxAuthenticationMode.ApiKey
                ? string.Equals(context.Request.Headers["OPEN-SANDBOX-API-KEY"], "test-key", StringComparison.Ordinal)
                : string.Equals(context.Request.Headers.Authorization, "Bearer test-token", StringComparison.Ordinal);

            if (!authorized)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = new { code = "Unauthorized", message = "auth required" } });
                return;
            }

            await next();
        });

        app.MapGet("/v1/ping", () => Results.Ok(new { ok = true }));
        app.MapGet("/v1/sandboxes", (HttpContext context) => Results.Ok(new ListSandboxesResult
        {
            Items =
            [
                new Sandbox
                {
                    Id = "sb-1",
                    Image = new SandboxImageReference { Uri = "ghcr.io/demo/image:latest" },
                    Status = new SandboxStatus { State = "Running" },
                    Metadata = new Dictionary<string, string> { ["tenant"] = context.Request.Query["metadata.tenant"].ToString() },
                    Entrypoint = new List<string> { "bash" }
                }
            ],
            Pagination = new PaginationInfo
            {
                Page = 1,
                PageSize = 10,
                TotalItems = 1,
                TotalPages = 1,
                HasNextPage = false
            }
        }));
        app.MapPost("/v1/sandboxes", async (HttpContext context) =>
        {
            var request = await context.Request.ReadFromJsonAsync<CreateSandboxRequest>();
            return Results.Ok(new CreatedSandbox
            {
                Id = "sb-created",
                Status = new SandboxStatus { State = "Running" },
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = request?.NeverExpires == true ? null : DateTimeOffset.UtcNow.AddMinutes(10),
                NeverExpires = request?.NeverExpires == true,
                Metadata = request?.Metadata,
                Entrypoint = request?.Entrypoint ?? new List<string>()
            });
        });
        app.MapGet("/v1/sandboxes/{id}", (string id) => Results.Ok(new Sandbox
        {
            Id = id,
            Image = new SandboxImageReference { Uri = "ghcr.io/demo/image:latest" },
            Status = new SandboxStatus { State = "Running" },
            Entrypoint = new List<string> { "bash" }
        }));
        app.MapGet("/v1/sandboxes/{id}/stats", (string id) => Results.Ok(new SandboxUsage
        {
            CpuPercent = 12.5m,
            MemoryPercent = 20m,
            CpuLimit = "500m",
            MemoryUsage = "100Mi",
            MemoryLimit = "512Mi",
            CollectedAt = DateTimeOffset.UtcNow
        }));
        app.MapGet("/v1/sandboxes/{id}/endpoints/{port:int}", (string id, int port) => Results.Ok(new SandboxEndpoint
        {
            Endpoint = $"demo.local:{port}",
            Url = $"https://demo.local/{id}",
            Headers = new Dictionary<string, string> { ["OPEN-SANDBOX-API-KEY"] = "test-key" }
        }));
        app.MapGet("/v1/sandboxes/{id}/logs", (string id) => Results.Ok(new SandboxLogsResult
        {
            Lines = new List<string> { "line-1", "line-2" }
        }));
        app.MapPost("/v1/sandboxes/{id}/exec", async (HttpContext context, string id) =>
        {
            _ = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(context.Request.Body);
            return Results.Ok(new CommandExecutionResult { ExitCode = 0, StdOut = "hi", StdErr = string.Empty });
        });
        app.MapPost("/v1/sandboxes/{id}/pause", (string id) => Results.Ok(new Sandbox
        {
            Id = id,
            Status = new SandboxStatus { State = "Paused" }
        }));
        app.MapPost("/v1/sandboxes/{id}/resume", (string id) => Results.Ok(new Sandbox
        {
            Id = id,
            Status = new SandboxStatus { State = "Running" }
        }));
        app.MapPost("/v1/sandboxes/{id}/renew-expiration", async (HttpContext context, string id) =>
        {
            var payload = await context.Request.ReadFromJsonAsync<Dictionary<string, string>>();
            return Results.Ok(new RenewSandboxExpirationResult
            {
                ExpiresAt = DateTimeOffset.Parse(payload!["expiresAt"])
            });
        });
        app.MapDelete("/v1/sandboxes/{id}", (string id) => Results.NoContent());

        await app.StartAsync();
        return app;
    }
}
