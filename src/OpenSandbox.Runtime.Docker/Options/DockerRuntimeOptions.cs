namespace OpenSandbox.Runtime.Docker.Options;

public sealed class DockerRuntimeOptions
{
    public const string SectionName = "DockerRuntime";

    public string DockerCommand { get; set; } = "docker";

    /// <summary>
    /// Name of the shared Docker network used by sandboxes created with networkPolicy.defaultAction = "Internal".
    /// Created on demand with --internal (no outbound access, container-to-container allowed).
    /// </summary>
    public string InternalNetworkName { get; set; } = "opensandbox-internal";

    public List<int> PublishedPorts { get; set; } =
    [
        80,
        443,
        3000,
        4173,
        5000,
        5050,
        5173,
        8000,
        8080,
        8081,
        8787,
        18789
    ];
}
