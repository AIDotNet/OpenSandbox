using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenSandbox.OpenClaw.Data;
using OpenSandbox.OpenClaw.Domain;
using OpenSandbox.OpenClaw.Options;

namespace OpenSandbox.OpenClaw.Services;

public sealed class SandboxServerHealthBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<OpenClawOptions> options,
    ILogger<SandboxServerHealthBackgroundService> logger) : BackgroundService
{
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(Math.Max(15, options.Value.SandboxHealthCheckIntervalSeconds));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to synchronize sandbox server health.");
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                {
                    break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task SyncOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OpenClawDbContext>();
        var gateway = scope.ServiceProvider.GetRequiredService<OpenSandboxGateway>();
        var servers = await dbContext.SandboxServers.Where(x => x.IsEnabled).ToListAsync(cancellationToken);
        if (servers.Count == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var server in servers)
        {
            try
            {
                var ok = await gateway.PingAsync(server.BaseUrl, server.ApiToken, cancellationToken);
                server.HealthStatus = ok ? SandboxServerStatus.Healthy : SandboxServerStatus.Unhealthy;
                server.LastHealthMessage = ok ? "OK" : "Ping failed";
            }
            catch (Exception ex)
            {
                server.HealthStatus = SandboxServerStatus.Unhealthy;
                server.LastHealthMessage = ex.Message;
            }

            server.LastCheckedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
