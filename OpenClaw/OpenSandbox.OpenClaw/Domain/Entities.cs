namespace OpenSandbox.OpenClaw.Domain;

public enum UserRole
{
    Admin = 1,
    Employee = 2
}

public enum UserStatus
{
    Active = 1,
    Disabled = 2
}

public enum SandboxServerStatus
{
    Unknown = 0,
    Healthy = 1,
    Unhealthy = 2
}

public sealed class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Employee;
    public UserStatus Status { get; set; } = UserStatus.Active;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class SandboxServerNode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiToken { get; set; } = string.Empty;
    public string PersistentRootPath { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public SandboxServerStatus HealthStatus { get; set; } = SandboxServerStatus.Unknown;
    public string? LastHealthMessage { get; set; }
    public DateTimeOffset? LastCheckedAt { get; set; }
}

public sealed class SystemSettings
{
    public int Id { get; set; } = 1;
    public string DefaultCpu { get; set; } = "1000m";
    public string DefaultMemory { get; set; } = "2Gi";
    public int DefaultLogTailLines { get; set; } = 200;
}

public sealed class DeploymentTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsBuiltin { get; set; }
    public bool IsEnabled { get; set; } = true;
    public Guid? CurrentVersionId { get; set; }
    public List<DeploymentTemplateVersion> Versions { get; set; } = new();
}

public sealed class DeploymentTemplateVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TemplateId { get; set; }
    public DeploymentTemplate? Template { get; set; }
    public string Version { get; set; } = "v1";
    public string Image { get; set; } = string.Empty;
    public int ContainerPort { get; set; }
    public string CommandJson { get; set; } = "[]";
    public string ConfigMountPath { get; set; } = "/app/config";
    public string ConfigFileName { get; set; } = "openclaw.json";
    public string WorkspaceMountPath { get; set; } = "/app/data";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class DeploymentInstance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public AppUser? User { get; set; }
    public Guid SandboxServerId { get; set; }
    public SandboxServerNode? SandboxServer { get; set; }
    public Guid TemplateId { get; set; }
    public Guid TemplateVersionId { get; set; }
    public string TemplateSnapshotJson { get; set; } = string.Empty;
    public string ApiEndpoint { get; set; } = string.Empty;
    public string ApiType { get; set; } = "chat";
    public string Model { get; set; } = string.Empty;
    public string ApiKeyCipherText { get; set; } = string.Empty;
    public string? SandboxId { get; set; }
    public string? ContainerId { get; set; }
    public string PersistentDirectory { get; set; } = string.Empty;
    public string ConfigFilePath { get; set; } = string.Empty;
    public bool NeverExpires { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
