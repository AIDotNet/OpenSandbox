using System.Net.WebSockets;
using OpenSandbox.Sdk.Models;

namespace OpenSandbox.Sdk;

public interface IOpenSandboxClient
{
    Task<bool> PingAsync(CancellationToken cancellationToken = default);
    Task<ListSandboxesResult> ListSandboxesAsync(ListSandboxesRequest? request = null, CancellationToken cancellationToken = default);
    Task<CreatedSandbox> CreateSandboxAsync(CreateSandboxRequest request, CancellationToken cancellationToken = default);
    Task<Sandbox?> GetSandboxAsync(string sandboxId, CancellationToken cancellationToken = default);
    Task<SandboxUsage?> GetSandboxUsageAsync(string sandboxId, CancellationToken cancellationToken = default);
    Task<SandboxEndpoint?> GetSandboxEndpointAsync(string sandboxId, int port, bool useServerProxy = true, CancellationToken cancellationToken = default);
    Task<CommandExecutionResult?> ExecuteCommandAsync(string sandboxId, string command, CancellationToken cancellationToken = default);
    Task<SandboxLogsResult?> GetSandboxLogsAsync(string sandboxId, int tail = 200, CancellationToken cancellationToken = default);
    Task<SandboxFileListResult?> ListFilesAsync(string sandboxId, string path, CancellationToken cancellationToken = default);
    Task<SandboxFileReadResult?> ReadFileAsync(string sandboxId, string path, CancellationToken cancellationToken = default);
    Task<bool> WriteFileAsync(string sandboxId, WriteSandboxFileRequest request, CancellationToken cancellationToken = default);
    Task<bool> CreateDirectoryAsync(string sandboxId, CreateSandboxDirectoryRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeletePathAsync(string sandboxId, string path, bool recursive = false, CancellationToken cancellationToken = default);
    Task<Sandbox?> PauseSandboxAsync(string sandboxId, CancellationToken cancellationToken = default);
    Task<Sandbox?> ResumeSandboxAsync(string sandboxId, CancellationToken cancellationToken = default);
    Task<RenewSandboxExpirationResult?> RenewSandboxExpirationAsync(string sandboxId, DateTimeOffset expiresAt, CancellationToken cancellationToken = default);
    Task<bool> DeleteSandboxAsync(string sandboxId, CancellationToken cancellationToken = default);
    Uri BuildTerminalWebSocketUri(string sandboxId);
    Uri BuildLogsWebSocketUri(string sandboxId);
    Task<ClientWebSocket> ConnectTerminalAsync(string sandboxId, CancellationToken cancellationToken = default);
    Task<ClientWebSocket> ConnectLogsStreamAsync(string sandboxId, CancellationToken cancellationToken = default);
}
