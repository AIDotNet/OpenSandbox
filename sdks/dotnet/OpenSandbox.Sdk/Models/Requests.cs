namespace OpenSandbox.Sdk.Models;

public sealed class ListSandboxesRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public IReadOnlyCollection<string>? States { get; set; }
    public IReadOnlyDictionary<string, string>? Metadata { get; set; }
}

public sealed class CreateSandboxRequest
{
    public SandboxImageReference Image { get; set; } = new();
    public List<string>? Entrypoint { get; set; }
    public int Timeout { get; set; }
    public bool NeverExpires { get; set; }
    public SandboxResourceLimits? ResourceLimits { get; set; }
    public Dictionary<string, string>? Env { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
    public SandboxNetworkPolicy? NetworkPolicy { get; set; }
    public List<SandboxVolume>? Volumes { get; set; }
    public Dictionary<string, object?>? Extensions { get; set; }
}

public sealed class WriteSandboxFileRequest
{
    public string Path { get; set; } = string.Empty;
    public string ContentBase64 { get; set; } = string.Empty;
}

public sealed class CreateSandboxDirectoryRequest
{
    public string Path { get; set; } = string.Empty;
}
