using System.Net.WebSockets;
using OpenSandbox.Abstractions.Contracts;

namespace OpenSandbox.Abstractions.Services;

public interface ISandboxRuntime
{
    Task<string> CreateAsync(SandboxRecord record, CancellationToken cancellationToken);
    Task DeleteAsync(string containerName, CancellationToken cancellationToken);
    Task PauseAsync(string containerName, CancellationToken cancellationToken);
    Task ResumeAsync(string containerName, CancellationToken cancellationToken);
    Task RestartAsync(string containerName, CancellationToken cancellationToken);
    Task<SandboxRuntimeState?> InspectAsync(string containerName, CancellationToken cancellationToken);
    Task<int?> GetPublishedPortAsync(string containerName, int containerPort, CancellationToken cancellationToken);
    Task<SandboxRuntimeUsage?> GetUsageAsync(string containerName, CancellationToken cancellationToken);
    Task<SandboxCommandResult?> ExecuteAsync(string containerName, string command, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<SandboxFileEntry>> ListFilesAsync(string containerName, string path, CancellationToken cancellationToken);
    Task<SandboxFileReadResult?> ReadFileAsync(string containerName, string path, CancellationToken cancellationToken);
    Task WriteFileAsync(string containerName, string path, byte[] content, CancellationToken cancellationToken);
    Task CreateDirectoryAsync(string containerName, string path, CancellationToken cancellationToken);
    Task MovePathAsync(string containerName, string sourcePath, string destinationPath, CancellationToken cancellationToken);
    Task CopyPathAsync(string containerName, string sourcePath, string destinationPath, CancellationToken cancellationToken);
    Task DeletePathAsync(string containerName, string path, bool recursive, CancellationToken cancellationToken);
    Task RunTerminalSessionAsync(string containerName, WebSocket webSocket, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>?> GetLogsAsync(string containerName, int tail, CancellationToken cancellationToken);
    Task StreamLogsAsync(string containerName, WebSocket webSocket, CancellationToken cancellationToken);
}
