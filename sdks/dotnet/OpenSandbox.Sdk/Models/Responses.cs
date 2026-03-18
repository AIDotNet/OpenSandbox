namespace OpenSandbox.Sdk.Models;

public sealed class Sandbox
{
    public string Id { get; set; } = string.Empty;
    public SandboxImageReference? Image { get; set; }
    public List<string> Entrypoint { get; set; } = new();
    public Dictionary<string, string>? Metadata { get; set; }
    public SandboxStatus? Status { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public bool NeverExpires { get; set; }
}

public sealed class CreatedSandbox
{
    public string Id { get; set; } = string.Empty;
    public SandboxStatus? Status { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public bool NeverExpires { get; set; }
    public List<string> Entrypoint { get; set; } = new();
}

public sealed class ListSandboxesResult
{
    public List<Sandbox> Items { get; set; } = new();
    public PaginationInfo Pagination { get; set; } = new();
}

public sealed class SandboxUsage
{
    public decimal? CpuPercent { get; set; }
    public decimal? MemoryPercent { get; set; }
    public string? CpuLimit { get; set; }
    public string? MemoryUsage { get; set; }
    public string? MemoryLimit { get; set; }
    public DateTimeOffset CollectedAt { get; set; }
}

public sealed class SandboxEndpoint
{
    public string? Endpoint { get; set; }
    public string? Url { get; set; }
    public Dictionary<string, string>? Headers { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}

public sealed class CommandExecutionResult
{
    public int ExitCode { get; set; }
    public string StdOut { get; set; } = string.Empty;
    public string StdErr { get; set; } = string.Empty;
}

public sealed class RenewSandboxExpirationResult
{
    public DateTimeOffset? ExpiresAt { get; set; }
    public bool NeverExpires { get; set; }
}

public sealed class SandboxLogsResult
{
    public List<string> Lines { get; set; } = new();
}

public sealed class SandboxFileEntry
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public long? SizeBytes { get; set; }
    public DateTimeOffset? LastModifiedAt { get; set; }
}

public sealed class SandboxFileListResult
{
    public string Path { get; set; } = "/";
    public List<SandboxFileEntry> Entries { get; set; } = new();
}

public sealed class SandboxFileReadResult
{
    public string Path { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentBase64 { get; set; } = string.Empty;
}
