using System.Diagnostics;
using System.Globalization;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenSandbox.Abstractions;
using OpenSandbox.Abstractions.Contracts;
using OpenSandbox.Abstractions.Services;
using OpenSandbox.Runtime.Docker.Options;

namespace OpenSandbox.Runtime.Docker;

public sealed class DockerSandboxRuntime : ISandboxRuntime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly DockerRuntimeOptions _options;
    private readonly ILogger<DockerSandboxRuntime> _logger;

    public DockerSandboxRuntime(IOptions<DockerRuntimeOptions> options, ILogger<DockerSandboxRuntime> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> CreateAsync(SandboxRecord record, CancellationToken cancellationToken)
    {
        var arguments = new List<string>
        {
            "run",
            "-d",
            "--name", record.ContainerName,
            "--label", "opensandbox.managed=true",
            "--label", $"opensandbox.id={record.Id}"
        };

        foreach (var port in _options.PublishedPorts.Distinct())
        {
            arguments.Add("-p");
            arguments.Add($"0:{port}");
        }

        var cpuLimit = ConvertCpu(record.ResourceLimits?.Cpu);
        if (!string.IsNullOrWhiteSpace(cpuLimit))
        {
            arguments.Add("--cpus");
            arguments.Add(cpuLimit);
        }

        var memoryLimit = ConvertMemory(record.ResourceLimits?.Memory);
        if (!string.IsNullOrWhiteSpace(memoryLimit))
        {
            arguments.Add("--memory");
            arguments.Add(memoryLimit);
        }

        if (record.Env != null)
        {
            foreach (var item in record.Env)
            {
                arguments.Add("-e");
                arguments.Add($"{item.Key}={item.Value}");
            }
        }

        if (record.Metadata != null)
        {
            foreach (var item in record.Metadata)
            {
                arguments.Add("--label");
                arguments.Add($"opensandbox.meta.{item.Key}={item.Value}");
            }
        }

        if (record.Volumes != null)
        {
            foreach (var volume in record.Volumes)
            {
                if (string.IsNullOrWhiteSpace(volume.MountPath) || string.IsNullOrWhiteSpace(volume.Host?.Path))
                {
                    continue;
                }

                var hostPath = volume.Host.Path;
                if (!string.IsNullOrWhiteSpace(volume.SubPath))
                {
                    hostPath = Path.Combine(hostPath, volume.SubPath);
                }

                var mount = $"{hostPath}:{volume.MountPath}";
                if (volume.ReadOnly == true)
                {
                    mount += ":ro";
                }

                arguments.Add("-v");
                arguments.Add(mount);
            }
        }

        if (record.Entrypoint.Count > 0)
        {
            arguments.Add("--entrypoint");
            arguments.Add(record.Entrypoint[0]);
        }

        arguments.Add(record.Image);

        if (record.Entrypoint.Count > 1)
        {
            arguments.AddRange(record.Entrypoint.Skip(1));
        }

        var result = await ExecuteProcessAsync(arguments, cancellationToken, throwOnError: true);
        return result.StdOut.Trim();
    }

    public async Task DeleteAsync(string containerName, CancellationToken cancellationToken)
    {
        var result = await ExecuteProcessAsync(["rm", "-f", containerName], cancellationToken, throwOnError: false);
        if (result.ExitCode != 0 && !ContainsNoSuchContainer(result.StdErr))
        {
            throw new InvalidOperationException($"Failed to delete Docker container: {result.StdErr}");
        }
    }

    public async Task PauseAsync(string containerName, CancellationToken cancellationToken)
    {
        var result = await ExecuteProcessAsync(["pause", containerName], cancellationToken, throwOnError: false);
        if (result.ExitCode != 0 && !ContainsAlreadyPaused(result.StdErr))
        {
            throw new InvalidOperationException($"Failed to pause Docker container: {result.StdErr}");
        }
    }

    public async Task ResumeAsync(string containerName, CancellationToken cancellationToken)
    {
        var result = await ExecuteProcessAsync(["unpause", containerName], cancellationToken, throwOnError: false);
        if (result.ExitCode != 0 && !ContainsNotPaused(result.StdErr))
        {
            throw new InvalidOperationException($"Failed to resume Docker container: {result.StdErr}");
        }
    }

    public async Task RestartAsync(string containerName, CancellationToken cancellationToken)
    {
        var result = await ExecuteProcessAsync(["restart", containerName], cancellationToken, throwOnError: false);
        if (result.ExitCode != 0)
        {
            if (ContainsNoSuchContainer(result.StdErr))
            {
                throw new InvalidOperationException("Docker container not found.");
            }

            throw new InvalidOperationException($"Failed to restart Docker container: {result.StdErr}");
        }
    }

    public async Task<SandboxRuntimeState?> InspectAsync(string containerName, CancellationToken cancellationToken)
    {
        var result = await ExecuteProcessAsync(["inspect", containerName], cancellationToken, throwOnError: false);
        if (result.ExitCode != 0)
        {
            if (ContainsNoSuchContainer(result.StdErr))
            {
                return null;
            }

            throw new InvalidOperationException($"Failed to inspect Docker container: {result.StdErr}");
        }

        var items = JsonSerializer.Deserialize<List<DockerInspectResponse>>(result.StdOut, JsonOptions);
        var item = items?.FirstOrDefault();
        if (item == null)
        {
            return null;
        }

        var state = MapState(item.State);
        state.ContainerId = item.Id;
        return state;
    }

    public async Task<int?> GetPublishedPortAsync(string containerName, int containerPort, CancellationToken cancellationToken)
    {
        var result = await ExecuteProcessAsync(["port", containerName, $"{containerPort}/tcp"], cancellationToken, throwOnError: false);
        if (result.ExitCode != 0)
        {
            return ContainsNoSuchContainer(result.StdErr) ? null : null;
        }

        var lines = result.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var separator = line.LastIndexOf(':');
            if (separator < 0)
            {
                continue;
            }

            var portText = line[(separator + 1)..].Trim();
            if (int.TryParse(portText, out var port))
            {
                return port;
            }
        }

        return null;
    }

    public async Task<SandboxRuntimeUsage?> GetUsageAsync(string containerName, CancellationToken cancellationToken)
    {
        var result = await ExecuteProcessAsync(["stats", "--no-stream", "--format", "{{json .}}", containerName], cancellationToken, throwOnError: false);
        if (result.ExitCode != 0)
        {
            if (ContainsNoSuchContainer(result.StdErr))
            {
                return null;
            }

            throw new InvalidOperationException($"Failed to read Docker stats: {result.StdErr}");
        }

        var json = result.StdOut.Trim();
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        var item = JsonSerializer.Deserialize<DockerStatsResponse>(json, JsonOptions);
        if (item == null)
        {
            return null;
        }

        var (memoryUsage, memoryLimit) = ParseMemoryUsage(item.MemoryUsage);
        return new SandboxRuntimeUsage
        {
            CpuPercent = ParsePercent(item.CpuPercent),
            MemoryPercent = ParsePercent(item.MemoryPercent),
            MemoryUsage = memoryUsage,
            MemoryLimit = memoryLimit,
            CollectedAt = DateTimeOffset.UtcNow
        };
    }

    public async Task<SandboxCommandResult?> ExecuteAsync(string containerName, string command, CancellationToken cancellationToken)
    {
        var result = await ExecuteProcessAsync(["exec", containerName, "sh", "-lc", command], cancellationToken, throwOnError: false);
        if (result.ExitCode != 0 && ContainsNoSuchContainer(result.StdErr))
        {
            return null;
        }

        return new SandboxCommandResult
        {
            ExitCode = result.ExitCode,
            StdOut = result.StdOut,
            StdErr = result.StdErr
        };
    }

    public async Task<IReadOnlyCollection<SandboxFileEntry>> ListFilesAsync(string containerName, string path, CancellationToken cancellationToken)
    {
        var normalizedPath = NormalizeContainerPath(path);
        var command = $"target={EscapeShellArgument(normalizedPath)}; if [ ! -d \"$target\" ]; then exit 2; fi; for entry in \"$target\"/* \"$target\"/.[!.]*; do [ -e \"$entry\" ] || continue; name=$(basename \"$entry\"); if [ -d \"$entry\" ]; then modified=$(stat -c %Y \"$entry\" 2>/dev/null || echo 0); printf 'D\\t%s\\t%s\\n' \"$name\" \"$modified\"; else size=$(wc -c < \"$entry\" 2>/dev/null || echo 0); modified=$(stat -c %Y \"$entry\" 2>/dev/null || echo 0); printf 'F\\t%s\\t%s\\t%s\\n' \"$name\" \"$size\" \"$modified\"; fi; done";
        var result = await ExecuteProcessAsync(["exec", containerName, "sh", "-lc", command], cancellationToken, throwOnError: false);
        if (result.ExitCode != 0)
        {
            if (ContainsNoSuchContainer(result.StdErr))
            {
                return [];
            }

            if (result.ExitCode == 2 || ContainsNoSuchPath(result.StdErr))
            {
                throw new DirectoryNotFoundException($"Directory not found: {normalizedPath}");
            }

            throw new InvalidOperationException($"Failed to list files: {result.StdErr}");
        }

        var items = new List<SandboxFileEntry>();
        var lines = result.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var parts = line.Split('\t');
            if (parts.Length < 3)
            {
                continue;
            }

            var isDirectory = string.Equals(parts[0], "D", StringComparison.OrdinalIgnoreCase);
            var name = parts[1];
            if (name == "." || name == "..")
            {
                continue;
            }
            long? size = !isDirectory && parts.Length >= 4 && long.TryParse(parts[2], out var parsedSize) ? parsedSize : null;
            var modifiedRaw = isDirectory ? parts[2] : parts[3];
            DateTimeOffset? modifiedAt = long.TryParse(modifiedRaw, out var unixSeconds) && unixSeconds > 0
                ? DateTimeOffset.FromUnixTimeSeconds(unixSeconds)
                : null;

            items.Add(new SandboxFileEntry
            {
                Name = name,
                Path = CombineContainerPath(normalizedPath, name),
                IsDirectory = isDirectory,
                SizeBytes = size,
                LastModifiedAt = modifiedAt
            });
        }

        return items.OrderByDescending(item => item.IsDirectory).ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public async Task<SandboxFileReadResult?> ReadFileAsync(string containerName, string path, CancellationToken cancellationToken)
    {
        var normalizedPath = NormalizeContainerPath(path);
        var tempRoot = Path.Combine(Path.GetTempPath(), "opensandbox-read", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var destinationPath = Path.Combine(tempRoot, Path.GetFileName(normalizedPath.TrimEnd('/')));
            var result = await ExecuteProcessAsync(["cp", $"{containerName}:{normalizedPath}", destinationPath], cancellationToken, throwOnError: false);
            if (result.ExitCode != 0)
            {
                if (ContainsNoSuchContainer(result.StdErr) || ContainsNoSuchPath(result.StdErr))
                {
                    return null;
                }

                throw new InvalidOperationException($"Failed to read file: {result.StdErr}");
            }

            if (!File.Exists(destinationPath))
            {
                return null;
            }

            return new SandboxFileReadResult
            {
                Path = normalizedPath,
                FileName = Path.GetFileName(destinationPath),
                Content = await File.ReadAllBytesAsync(destinationPath, cancellationToken)
            };
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    public async Task WriteFileAsync(string containerName, string path, byte[] content, CancellationToken cancellationToken)
    {
        var normalizedPath = NormalizeContainerPath(path);
        var parentDirectory = Path.GetDirectoryName(normalizedPath.Replace('/', Path.DirectorySeparatorChar))?.Replace(Path.DirectorySeparatorChar, '/') ?? "/";
        var tempRoot = Path.Combine(Path.GetTempPath(), "opensandbox-write", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var tempFile = Path.Combine(tempRoot, Path.GetFileName(normalizedPath));
        try
        {
            await File.WriteAllBytesAsync(tempFile, content, cancellationToken);
            await ExecuteProcessAsync(["exec", containerName, "sh", "-lc", $"mkdir -p {EscapeShellArgument(parentDirectory)}"], cancellationToken, throwOnError: true);
            var copyResult = await ExecuteProcessAsync(["cp", tempFile, $"{containerName}:{normalizedPath}"], cancellationToken, throwOnError: false);
            if (copyResult.ExitCode != 0)
            {
                throw new InvalidOperationException($"Failed to write file: {copyResult.StdErr}");
            }
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    public async Task CreateDirectoryAsync(string containerName, string path, CancellationToken cancellationToken)
    {
        var normalizedPath = NormalizeContainerPath(path);
        await ExecuteProcessAsync(["exec", containerName, "sh", "-lc", $"mkdir -p {EscapeShellArgument(normalizedPath)}"], cancellationToken, throwOnError: true);
    }

    public async Task MovePathAsync(string containerName, string sourcePath, string destinationPath, CancellationToken cancellationToken)
    {
        var normalizedSourcePath = NormalizeContainerPath(sourcePath);
        var normalizedDestinationPath = NormalizeContainerPath(destinationPath);
        var destinationDirectory = NormalizeContainerPath(Path.GetDirectoryName(normalizedDestinationPath.Replace('/', Path.DirectorySeparatorChar))?.Replace(Path.DirectorySeparatorChar, '/') ?? "/");
        var command = $"mkdir -p {EscapeShellArgument(destinationDirectory)} && mv {EscapeShellArgument(normalizedSourcePath)} {EscapeShellArgument(normalizedDestinationPath)}";
        var result = await ExecuteProcessAsync(["exec", containerName, "sh", "-lc", command], cancellationToken, throwOnError: false);
        if (result.ExitCode != 0 && !ContainsNoSuchContainer(result.StdErr))
        {
            throw new InvalidOperationException($"Failed to move path: {result.StdErr}");
        }
    }

    public async Task CopyPathAsync(string containerName, string sourcePath, string destinationPath, CancellationToken cancellationToken)
    {
        var normalizedSourcePath = NormalizeContainerPath(sourcePath);
        var normalizedDestinationPath = NormalizeContainerPath(destinationPath);
        var destinationDirectory = NormalizeContainerPath(Path.GetDirectoryName(normalizedDestinationPath.Replace('/', Path.DirectorySeparatorChar))?.Replace(Path.DirectorySeparatorChar, '/') ?? "/");
        var command = $"mkdir -p {EscapeShellArgument(destinationDirectory)} && if [ -d {EscapeShellArgument(normalizedSourcePath)} ]; then cp -R {EscapeShellArgument(normalizedSourcePath)} {EscapeShellArgument(normalizedDestinationPath)}; else cp {EscapeShellArgument(normalizedSourcePath)} {EscapeShellArgument(normalizedDestinationPath)}; fi";
        var result = await ExecuteProcessAsync(["exec", containerName, "sh", "-lc", command], cancellationToken, throwOnError: false);
        if (result.ExitCode != 0 && !ContainsNoSuchContainer(result.StdErr))
        {
            throw new InvalidOperationException($"Failed to copy path: {result.StdErr}");
        }
    }

    public async Task DeletePathAsync(string containerName, string path, bool recursive, CancellationToken cancellationToken)
    {
        var normalizedPath = NormalizeContainerPath(path);
        var flag = recursive ? "-rf" : "-f";
        var result = await ExecuteProcessAsync(["exec", containerName, "sh", "-lc", $"rm {flag} {EscapeShellArgument(normalizedPath)}"], cancellationToken, throwOnError: false);
        if (result.ExitCode != 0 && !ContainsNoSuchPath(result.StdErr) && !ContainsNoSuchContainer(result.StdErr))
        {
            throw new InvalidOperationException($"Failed to delete path: {result.StdErr}");
        }
    }

    public async Task RunTerminalSessionAsync(string containerName, WebSocket webSocket, CancellationToken cancellationToken)
    {
        if (OperatingSystem.IsWindows())
        {
            await RunWindowsTerminalSessionAsync(containerName, webSocket, cancellationToken);
            return;
        }

        using var process = StartInteractiveProcess(["exec", "-i", containerName, "sh", "-lc", "export TERM=xterm-256color; if command -v bash >/dev/null 2>&1; then exec bash -i; else exec sh -i; fi"]);

        using var sendLock = new SemaphoreSlim(1, 1);
        var stdoutTask = PumpStreamToWebSocketAsync(process.StandardOutput.BaseStream, webSocket, sendLock, cancellationToken);
        var stderrTask = PumpStreamToWebSocketAsync(process.StandardError.BaseStream, webSocket, sendLock, cancellationToken);
        var stdinTask = PumpWebSocketToStreamAsync(webSocket, process.StandardInput, null, cancellationToken);

        await Task.WhenAny(stdoutTask, stderrTask, stdinTask, process.WaitForExitAsync(cancellationToken));

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }

        if (webSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            try
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "terminal closed", CancellationToken.None);
            }
            catch
            {
            }
        }
    }

    private async Task RunWindowsTerminalSessionAsync(string containerName, WebSocket webSocket, CancellationToken cancellationToken)
    {
        using var process = WindowsConPtyProcess.Start(_options.DockerCommand,
            ["exec", "-it", containerName, "sh", "-lc", "export TERM=xterm-256color; if command -v bash >/dev/null 2>&1; then exec bash -i; else exec sh -i; fi"],
            120,
            30);

        using var sendLock = new SemaphoreSlim(1, 1);
        var outputTask = PumpStreamToWebSocketAsync(process.OutputStream, webSocket, sendLock, cancellationToken);
        var inputTask = PumpWebSocketToStreamAsync(webSocket, process.InputWriter, process.Resize, cancellationToken);

        await Task.WhenAny(outputTask, inputTask, process.WaitForExitAsync(cancellationToken));

        process.Terminate();

        if (webSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            try
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "terminal closed", CancellationToken.None);
            }
            catch
            {
            }
        }
    }

    public async Task<IReadOnlyList<string>?> GetLogsAsync(string containerName, int tail, CancellationToken cancellationToken)
    {
        var normalizedTail = Math.Clamp(tail, 1, 2000);
        var result = await ExecuteProcessAsync(["logs", "--tail", normalizedTail.ToString(CultureInfo.InvariantCulture), containerName], cancellationToken, throwOnError: false);
        if (result.ExitCode != 0)
        {
            if (ContainsNoSuchContainer(result.StdErr))
            {
                return null;
            }

            throw new InvalidOperationException($"Failed to read Docker logs: {result.StdErr}");
        }

        return result.StdOut
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .ToList();
    }

    public async Task StreamLogsAsync(string containerName, WebSocket webSocket, CancellationToken cancellationToken)
    {
        using var process = StartInteractiveProcess(["logs", "-f", "--tail", "200", containerName], redirectInput: false);
        using var sendLock = new SemaphoreSlim(1, 1);
        var stdoutTask = PumpStreamToWebSocketAsync(process.StandardOutput.BaseStream, webSocket, sendLock, cancellationToken);
        var stderrTask = PumpStreamToWebSocketAsync(process.StandardError.BaseStream, webSocket, sendLock, cancellationToken);
        await Task.WhenAny(stdoutTask, stderrTask, process.WaitForExitAsync(cancellationToken));

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }

        if (webSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            try
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "logs closed", CancellationToken.None);
            }
            catch
            {
            }
        }
    }

    private async Task<ProcessResult> ExecuteProcessAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken, bool throwOnError)
    {
        using var process = StartInteractiveProcess(arguments, redirectInput: false);

        var stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        var stdOut = await stdOutTask;
        var stdErr = await stdErrTask;
        if (throwOnError && process.ExitCode != 0)
        {
            _logger.LogError("Docker command failed: {Arguments}; stderr={StdErr}", string.Join(' ', arguments), stdErr);
            throw new InvalidOperationException($"Docker command failed: {stdErr}");
        }

        return new ProcessResult(process.ExitCode, stdOut, stdErr);
    }

    private Process StartInteractiveProcess(IReadOnlyList<string> arguments, bool redirectInput = true)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _options.DockerCommand,
            RedirectStandardInput = redirectInput,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        var process = new Process { StartInfo = startInfo };
        process.Start();
        return process;
    }

    private static SandboxRuntimeState MapState(DockerStateResponse? state)
    {
        var status = state?.Status?.ToLowerInvariant();
        var mappedState = status switch
        {
            "created" => SandboxStateNames.Creating,
            "running" => state?.Paused == true ? SandboxStateNames.Paused : SandboxStateNames.Running,
            "paused" => SandboxStateNames.Paused,
            "restarting" => SandboxStateNames.Resuming,
            "removing" => SandboxStateNames.Deleting,
            "dead" => SandboxStateNames.Error,
            "exited" => SandboxStateNames.Error,
            _ => SandboxStateNames.Error
        };

        var message = state?.Error;
        if (string.IsNullOrWhiteSpace(message) && string.Equals(status, "exited", StringComparison.OrdinalIgnoreCase))
        {
            message = $"Container exited with code {state?.ExitCode}";
        }

        return new SandboxRuntimeState
        {
            State = mappedState,
            Reason = status,
            Message = message
        };
    }

    private static string? ConvertCpu(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        value = value.Trim();
        if (value.EndsWith("m", StringComparison.OrdinalIgnoreCase)
            && decimal.TryParse(value[..^1], CultureInfo.InvariantCulture, out var milli))
        {
            return (milli / 1000m).ToString("0.###", CultureInfo.InvariantCulture);
        }

        if (decimal.TryParse(value, CultureInfo.InvariantCulture, out var cpu))
        {
            return cpu.ToString("0.###", CultureInfo.InvariantCulture);
        }

        return null;
    }

    private static string? ConvertMemory(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        value = value.Trim();
        if (value.EndsWith("Gi", StringComparison.OrdinalIgnoreCase))
        {
            return value[..^2] + "g";
        }

        if (value.EndsWith("Mi", StringComparison.OrdinalIgnoreCase))
        {
            return value[..^2] + "m";
        }

        if (value.EndsWith("G", StringComparison.OrdinalIgnoreCase) || value.EndsWith("M", StringComparison.OrdinalIgnoreCase))
        {
            return value.ToLowerInvariant();
        }

        return value;
    }

    private static decimal? ParsePercent(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        value = value.Trim().TrimEnd('%');
        return decimal.TryParse(value, CultureInfo.InvariantCulture, out var result) ? result : null;
    }

    private static (string? Usage, string? Limit) ParseMemoryUsage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return (null, null);
        }

        var parts = value.Split(" / ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            >= 2 => (parts[0], parts[1]),
            1 => (parts[0], null),
            _ => (null, null)
        };
    }

    private static async Task PumpStreamToWebSocketAsync(Stream stream, WebSocket webSocket, SemaphoreSlim sendLock, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        while (!cancellationToken.IsCancellationRequested && (webSocket.State == WebSocketState.Open || webSocket.State == WebSocketState.CloseReceived))
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read <= 0)
            {
                break;
            }

            await sendLock.WaitAsync(cancellationToken);
            try
            {
                await webSocket.SendAsync(buffer.AsMemory(0, read), WebSocketMessageType.Text, true, cancellationToken);
            }
            finally
            {
                sendLock.Release();
            }
        }
    }

    private static async Task PumpWebSocketToStreamAsync(WebSocket webSocket, StreamWriter writer, Action<int, int>? resizeCallback, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        while (!cancellationToken.IsCancellationRequested && webSocket.State == WebSocketState.Open)
        {
            using var messageBuffer = new MemoryStream();
            bool endOfMessage;
            do
            {
                var result = await webSocket.ReceiveAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return;
                }

                messageBuffer.Write(buffer, 0, result.Count);
                endOfMessage = result.EndOfMessage;
            }
            while (!endOfMessage);

            var text = Encoding.UTF8.GetString(messageBuffer.GetBuffer(), 0, (int)messageBuffer.Length);
            if (await TryHandleTerminalClientMessageAsync(text, writer, resizeCallback, cancellationToken))
            {
                continue;
            }

            await writer.WriteAsync(text);
            await writer.FlushAsync(cancellationToken);
        }
    }

    private static async Task<bool> TryHandleTerminalClientMessageAsync(string payload, StreamWriter writer, Action<int, int>? resizeCallback, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payload) || payload[0] != '{')
        {
            return false;
        }

        TerminalClientMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<TerminalClientMessage>(payload, JsonOptions);
        }
        catch (JsonException)
        {
            return false;
        }

        if (message == null)
        {
            return false;
        }

        if (string.Equals(message.Type, "input", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrEmpty(message.Data))
            {
                await writer.WriteAsync(message.Data);
                await writer.FlushAsync(cancellationToken);
            }

            return true;
        }

        if (string.Equals(message.Type, "resize", StringComparison.OrdinalIgnoreCase))
        {
            if (message.Cols is > 0 && message.Rows is > 0)
            {
                resizeCallback?.Invoke(message.Cols.Value, message.Rows.Value);
            }

            return true;
        }

        return false;
    }

    private sealed class TerminalClientMessage
    {
        public string? Type { get; set; }
        public string? Data { get; set; }
        public int? Cols { get; set; }
        public int? Rows { get; set; }
    }

    private static bool ContainsNoSuchContainer(string message)
    {
        return message.Contains("No such container", StringComparison.OrdinalIgnoreCase)
               || message.Contains("No such object", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsAlreadyPaused(string message)
    {
        return message.Contains("is already paused", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsNotPaused(string message)
    {
        return message.Contains("is not paused", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsNoSuchPath(string message)
    {
        return message.Contains("Could not find the file", StringComparison.OrdinalIgnoreCase)
               || message.Contains("No such file or directory", StringComparison.OrdinalIgnoreCase)
               || message.Contains("file does not exist", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeContainerPath(string path)
    {
        var normalized = string.IsNullOrWhiteSpace(path) ? "/" : path.Trim();
        normalized = normalized.Replace('\\', '/');
        if (!normalized.StartsWith('/'))
        {
            normalized = "/" + normalized;
        }

        return normalized.Length > 1 ? normalized.TrimEnd('/') : normalized;
    }

    private static string CombineContainerPath(string parent, string child)
    {
        var normalizedParent = NormalizeContainerPath(parent);
        if (normalizedParent == "/")
        {
            return "/" + child.TrimStart('/');
        }

        return normalizedParent + "/" + child.TrimStart('/');
    }

    private static string EscapeShellArgument(string value)
    {
        return $"'{value.Replace("'", "'\\''")}'";
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }
}
