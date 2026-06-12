using System.Text.Json.Serialization;

namespace OpenSandbox.Server.Contracts;

public sealed class PingResponse
{
}

public sealed class ImageSpec
{
    public string Uri { get; set; } = string.Empty;
}

public sealed class ResourceLimits
{
    public string? Cpu { get; set; }
    public string? Memory { get; set; }
}

public sealed class HostVolume
{
    public string Path { get; set; } = string.Empty;
}

public sealed class VolumeSpec
{
    public string Name { get; set; } = string.Empty;
    public string MountPath { get; set; } = string.Empty;
    public bool? ReadOnly { get; set; }
    public string? SubPath { get; set; }
    public HostVolume? Host { get; set; }
}

public sealed class NetworkPolicy
{
    public string? DefaultAction { get; set; }
}

public sealed class CreateSandboxRequest
{
    public ImageSpec? Image { get; set; }
    public List<string>? Entrypoint { get; set; }
    public int Timeout { get; set; }
    public bool NeverExpires { get; set; }
    public ResourceLimits? ResourceLimits { get; set; }
    public Dictionary<string, string>? Env { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
    public NetworkPolicy? NetworkPolicy { get; set; }
    public List<VolumeSpec>? Volumes { get; set; }
    public Dictionary<string, object?>? Extensions { get; set; }
}

public sealed class SandboxStatus
{
    public string State { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string? Message { get; set; }
}

public sealed class SandboxInfoResponse
{
    public string Id { get; set; } = string.Empty;
    public string? ContainerId { get; set; }
    public ImageSpec? Image { get; set; }
    public List<string> Entrypoint { get; set; } = new();
    public Dictionary<string, string>? Metadata { get; set; }
    public SandboxStatus? Status { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public bool NeverExpires { get; set; }
}

public sealed class CreateSandboxResponse
{
    public string Id { get; set; } = string.Empty;
    public string? ContainerId { get; set; }
    public SandboxStatus? Status { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public bool NeverExpires { get; set; }
    public List<string> Entrypoint { get; set; } = new();
}

public sealed class PaginationInfo
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
    public bool HasNextPage { get; set; }
}

public sealed class ListSandboxesResponse
{
    public List<SandboxInfoResponse> Items { get; set; } = new();
    public PaginationInfo Pagination { get; set; } = new();
}

public sealed class RenewSandboxExpirationRequest
{
    public string? ExpiresAt { get; set; }
}

public sealed class RenewSandboxExpirationResponse
{
    public DateTimeOffset? ExpiresAt { get; set; }
    public bool NeverExpires { get; set; }
}

public sealed class EndpointResponse
{
    [JsonPropertyName("endpoint")]
    public string Endpoint { get; set; } = string.Empty;

    [JsonIgnore]
    public string EndpointAddress
    {
        get => Endpoint;
        set => Endpoint = value;
    }

    public Dictionary<string, string>? Headers { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("expiresAt")]
    public DateTimeOffset? ExpiresAt { get; set; }
}

public sealed class SandboxUsageResponse
{
    public decimal? CpuPercent { get; set; }
    public decimal? MemoryPercent { get; set; }
    public string? CpuLimit { get; set; }
    public string? MemoryUsage { get; set; }
    public string? MemoryLimit { get; set; }
    public DateTimeOffset CollectedAt { get; set; }
}

public sealed class ExecuteCommandRequest
{
    public string Command { get; set; } = string.Empty;
}

public sealed class ExecuteCommandResponse
{
    public int ExitCode { get; set; }
    public string StdOut { get; set; } = string.Empty;
    public string StdErr { get; set; } = string.Empty;
}

public sealed class ListFilesResponse
{
    public string Path { get; set; } = "/";
    public List<FileEntryResponse> Entries { get; set; } = new();
}

public sealed class FileEntryResponse
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public long? SizeBytes { get; set; }
    public DateTimeOffset? LastModifiedAt { get; set; }
}

public sealed class ReadFileResponse
{
    public string Path { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentBase64 { get; set; } = string.Empty;
}

public sealed class WriteFileRequest
{
    public string Path { get; set; } = string.Empty;
    public string ContentBase64 { get; set; } = string.Empty;
}

public sealed class CreateDirectoryRequest
{
    public string Path { get; set; } = string.Empty;
}

public sealed class MovePathRequest
{
    public string SourcePath { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;
}

public sealed class CopyPathRequest
{
    public string SourcePath { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;
}

public sealed class SandboxLogsResponse
{
    public List<string> Lines { get; set; } = new();
}
