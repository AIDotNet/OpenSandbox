namespace OpenSandbox.OpenClaw.Options;

public sealed class OpenClawOptions
{
    public const string SectionName = "OpenClaw";

    public string DatabasePath { get; set; } = "data/openclaw.db";
    public string CookieName { get; set; } = "openclaw.session";
    public string ApiKeyEncryptionKey { get; set; } = "change-this-key";
    public int SandboxHealthCheckIntervalSeconds { get; set; } = 60;
}
