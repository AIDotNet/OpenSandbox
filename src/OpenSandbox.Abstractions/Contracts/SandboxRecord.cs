namespace OpenSandbox.Abstractions.Contracts;

public sealed class SandboxRecord
{
    public string Id { get; set; } = string.Empty;
    public string ContainerName { get; set; } = string.Empty;
    public string? ContainerId { get; set; }
    public string Image { get; set; } = string.Empty;
    public List<string> Entrypoint { get; set; } = new();
    public Dictionary<string, string>? Metadata { get; set; }
    public Dictionary<string, string>? Env { get; set; }
    public List<SandboxVolumeSpec>? Volumes { get; set; }
    public SandboxNetworkPolicy? NetworkPolicy { get; set; }
    public SandboxResourceLimits? ResourceLimits { get; set; }
    public int? TimeoutSeconds { get; set; }
    public bool NeverExpires { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public string? LastKnownState { get; set; }
    public string? LastKnownReason { get; set; }
    public string? LastKnownMessage { get; set; }
}
