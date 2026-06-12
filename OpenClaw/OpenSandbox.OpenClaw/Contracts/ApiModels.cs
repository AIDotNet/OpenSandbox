namespace OpenSandbox.OpenClaw.Contracts;

public sealed record LoginRequest(string UserName, string Password);
public sealed record CurrentUserResponse(Guid Id, string UserName, string DisplayName, string Role);
public sealed record CreateUserRequest(string UserName, string DisplayName, string Password, string Role);
public sealed record UpdateUserRequest(string DisplayName, string Role, string Status);
public sealed record ResetPasswordRequest(string Password);
public sealed record SandboxServerRequest(string Name, string BaseUrl, string ApiToken, string PersistentRootPath, bool IsEnabled);
public sealed record SystemSettingsRequest(string DefaultCpu, string DefaultMemory, int DefaultLogTailLines);
public sealed record TemplateRequest(string Name, string Description, bool IsEnabled);
public sealed record TemplateVersionRequest(string Version, string Image, int ContainerPort, List<string> Command, string ConfigMountPath, string ConfigFileName, string WorkspaceMountPath, bool IsActive);
public sealed record DeployRequest(Guid SandboxServerId, Guid TemplateId, string ApiEndpoint, string ApiType, string Model, string ApiKey);
public sealed record DeploymentWriteFileRequest(string Path, string ContentBase64);
public sealed record DeploymentCreateDirectoryRequest(string Path);
public sealed record DeploymentMovePathRequest(string SourcePath, string DestinationPath);
public sealed record DeploymentCopyPathRequest(string SourcePath, string DestinationPath);
