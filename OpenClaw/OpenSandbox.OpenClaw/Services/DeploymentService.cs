using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpenSandbox.OpenClaw.Contracts;
using OpenSandbox.OpenClaw.Data;
using OpenSandbox.OpenClaw.Domain;
using OpenSandbox.Server.Contracts;

namespace OpenSandbox.OpenClaw.Services;

public sealed class DeploymentService(
    OpenClawDbContext dbContext,
    OpenSandboxGateway gateway,
    SecretProtector secretProtector)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<object> UpsertDeploymentAsync(Guid userId, DeployRequest request, CancellationToken cancellationToken)
    {
        var server = await dbContext.SandboxServers.FirstOrDefaultAsync(x => x.Id == request.SandboxServerId && x.IsEnabled, cancellationToken)
            ?? throw new InvalidOperationException("Sandbox server not found.");
        var template = await dbContext.Templates.FirstOrDefaultAsync(x => x.Id == request.TemplateId && x.IsEnabled, cancellationToken)
            ?? throw new InvalidOperationException("Template not found.");
        var templateVersion = await dbContext.TemplateVersions.FirstOrDefaultAsync(x => x.TemplateId == template.Id && x.Id == template.CurrentVersionId, cancellationToken)
            ?? throw new InvalidOperationException("Template version not found.");
        var settings = await dbContext.SystemSettings.FirstAsync(cancellationToken);
        var user = await dbContext.Users.FirstAsync(x => x.Id == userId, cancellationToken);

        var apiType = NormalizeApiType(request.ApiType);
        var root = BuildPersistentDirectory(server.PersistentRootPath, user.UserName);
        Directory.CreateDirectory(root);

        var configFileName = NormalizeFileName(templateVersion.ConfigFileName, "openclaw.json");
        var configPath = BuildChildPath(root, configFileName);
        var config = BuildOpenClawConfig(
            request.ApiEndpoint,
            apiType,
            request.Model,
            request.ApiKey,
            templateVersion.WorkspaceMountPath);
        await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(config, JsonOptions), Encoding.UTF8, cancellationToken);

        var existing = await dbContext.DeploymentInstances.FirstOrDefaultAsync(x => x.UserId == userId && x.SandboxServerId == server.Id, cancellationToken);
        var canRestartInPlace = existing != null
            && !string.IsNullOrWhiteSpace(existing.SandboxId)
            && existing.TemplateId == template.Id
            && existing.TemplateVersionId == templateVersion.Id;

        SandboxInfoResponse? info;
        string sandboxId;

        if (canRestartInPlace)
        {
            info = await gateway.RestartSandboxAsync(server.BaseUrl, server.ApiToken, existing!.SandboxId!, cancellationToken);
            if (info == null)
            {
                var created = await gateway.CreateSandboxAsync(server.BaseUrl, server.ApiToken, BuildCreateRequest(template, templateVersion, settings, user, server, configPath, root), cancellationToken);
                sandboxId = created.Id;
                info = await gateway.GetSandboxAsync(server.BaseUrl, server.ApiToken, sandboxId, cancellationToken)
                    ?? new SandboxInfoResponse
                    {
                        Id = created.Id,
                        ContainerId = created.ContainerId,
                        CreatedAt = created.CreatedAt,
                        ExpiresAt = created.ExpiresAt,
                        NeverExpires = created.NeverExpires,
                        Status = created.Status
                    };
            }
            else
            {
                sandboxId = existing!.SandboxId!;
            }
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(existing?.SandboxId))
            {
                await gateway.DeleteSandboxAsync(server.BaseUrl, server.ApiToken, existing.SandboxId, cancellationToken);
            }

            var created = await gateway.CreateSandboxAsync(server.BaseUrl, server.ApiToken, BuildCreateRequest(template, templateVersion, settings, user, server, configPath, root), cancellationToken);
            sandboxId = created.Id;
            info = await gateway.GetSandboxAsync(server.BaseUrl, server.ApiToken, sandboxId, cancellationToken)
                ?? new SandboxInfoResponse
                {
                    Id = created.Id,
                    ContainerId = created.ContainerId,
                    CreatedAt = created.CreatedAt,
                    ExpiresAt = created.ExpiresAt,
                    NeverExpires = created.NeverExpires,
                    Status = created.Status
                };
        }

        var command = JsonSerializer.Deserialize<List<string>>(templateVersion.CommandJson, JsonOptions) ?? new List<string>();
        var instance = existing ?? new DeploymentInstance
        {
            UserId = userId,
            SandboxServerId = server.Id,
            CreatedAt = DateTimeOffset.UtcNow
        };

        instance.TemplateId = template.Id;
        instance.TemplateVersionId = templateVersion.Id;
        instance.TemplateSnapshotJson = JsonSerializer.Serialize(new
        {
            template.Name,
            template.Description,
            templateVersion.Version,
            templateVersion.Image,
            templateVersion.ContainerPort,
            Command = command,
            templateVersion.ConfigMountPath,
            templateVersion.ConfigFileName,
            templateVersion.WorkspaceMountPath
        }, JsonOptions);
        instance.ApiEndpoint = request.ApiEndpoint.Trim();
        instance.ApiType = apiType;
        instance.Model = request.Model.Trim();
        instance.ApiKeyCipherText = secretProtector.Protect(request.ApiKey);
        instance.SandboxId = sandboxId;
        instance.ContainerId = info?.ContainerId ?? instance.ContainerId;
        instance.PersistentDirectory = root;
        instance.ConfigFilePath = configPath;
        instance.NeverExpires = true;
        instance.UpdatedAt = DateTimeOffset.UtcNow;
        if (existing == null)
        {
            dbContext.DeploymentInstances.Add(instance);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetDeploymentDetailAsync(instance.Id, cancellationToken) ?? new { };
    }

    public async Task<object?> GetDeploymentDetailByUserAndServerAsync(Guid userId, Guid sandboxServerId, CancellationToken cancellationToken)
    {
        var instance = await dbContext.DeploymentInstances.FirstOrDefaultAsync(x => x.UserId == userId && x.SandboxServerId == sandboxServerId, cancellationToken);
        return instance == null ? null : await GetDeploymentDetailAsync(instance.Id, cancellationToken);
    }

    public async Task<bool> DeleteDeploymentAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        var instance = await dbContext.DeploymentInstances
            .Include(x => x.SandboxServer)
            .FirstOrDefaultAsync(x => x.Id == instanceId, cancellationToken);
        if (instance == null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(instance.SandboxId) && instance.SandboxServer != null)
        {
            await gateway.DeleteSandboxAsync(instance.SandboxServer.BaseUrl, instance.SandboxServer.ApiToken, instance.SandboxId, cancellationToken);
        }

        dbContext.DeploymentInstances.Remove(instance);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<object?> GetDeploymentDetailAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        var instance = await dbContext.DeploymentInstances
            .Include(x => x.User)
            .Include(x => x.SandboxServer)
            .FirstOrDefaultAsync(x => x.Id == instanceId, cancellationToken);
        if (instance == null)
        {
            return null;
        }

        SandboxInfoResponse? info = null;
        SandboxUsageResponse? stats = null;
        if (!string.IsNullOrWhiteSpace(instance.SandboxId) && instance.SandboxServer != null)
        {
            info = await gateway.GetSandboxAsync(instance.SandboxServer.BaseUrl, instance.SandboxServer.ApiToken, instance.SandboxId, cancellationToken);
            stats = await gateway.GetStatsAsync(instance.SandboxServer.BaseUrl, instance.SandboxServer.ApiToken, instance.SandboxId, cancellationToken);
        }

        return new
        {
            instance.Id,
            instance.SandboxId,
            ContainerId = info?.ContainerId ?? instance.ContainerId,
            instance.CreatedAt,
            instance.UpdatedAt,
            RuntimeCreatedAt = info?.CreatedAt,
            ExpiresAt = info?.ExpiresAt,
            NeverExpires = info?.NeverExpires ?? instance.NeverExpires,
            Status = info?.Status?.State,
            StatusReason = info?.Status?.Reason,
            StatusMessage = info?.Status?.Message,
            CpuPercent = stats?.CpuPercent,
            MemoryPercent = stats?.MemoryPercent,
            MemoryUsage = stats?.MemoryUsage,
            MemoryLimit = stats?.MemoryLimit,
            Server = instance.SandboxServer == null ? null : new { instance.SandboxServer.Id, instance.SandboxServer.Name, instance.SandboxServer.HealthStatus },
            User = instance.User == null ? null : new { instance.User.Id, instance.User.UserName, instance.User.DisplayName },
            ConfigSummary = new
            {
                instance.ApiEndpoint,
                instance.ApiType,
                instance.Model,
                instance.PersistentDirectory,
                instance.ConfigFilePath
            },
            TemplateSnapshot = string.IsNullOrWhiteSpace(instance.TemplateSnapshotJson) ? null : JsonSerializer.Deserialize<object>(instance.TemplateSnapshotJson, JsonOptions)
        };
    }

    private static CreateSandboxRequest BuildCreateRequest(
        DeploymentTemplate template,
        DeploymentTemplateVersion templateVersion,
        SystemSettings settings,
        AppUser user,
        SandboxServerNode server,
        string configPath,
        string root)
    {
        var command = JsonSerializer.Deserialize<List<string>>(templateVersion.CommandJson, JsonOptions) ?? new List<string>();
        return new CreateSandboxRequest
        {
            Image = new ImageSpec { Uri = templateVersion.Image },
            Entrypoint = command,
            NeverExpires = true,
            Metadata = new Dictionary<string, string>
            {
                ["userId"] = user.Id.ToString(),
                ["userName"] = user.UserName,
                ["templateId"] = template.Id.ToString(),
                ["templateVersionId"] = templateVersion.Id.ToString(),
                ["sandboxServerId"] = server.Id.ToString()
            },
            ResourceLimits = new ResourceLimits
            {
                Cpu = settings.DefaultCpu,
                Memory = settings.DefaultMemory
            },
            Volumes = new List<VolumeSpec>
            {
                new()
                {
                    Name = "openclaw-config",
                    MountPath = CombineMountPath(templateVersion.ConfigMountPath, NormalizeFileName(templateVersion.ConfigFileName, "openclaw.json")),
                    Host = new HostVolume { Path = configPath }
                },
                new()
                {
                    Name = "openclaw-data",
                    MountPath = NormalizeMountPath(templateVersion.WorkspaceMountPath),
                    Host = new HostVolume { Path = root }
                }
            }
        };
    }

    private static object BuildOpenClawConfig(string apiEndpoint, string apiType, string model, string apiKey, string workspaceMountPath)
    {
        var normalizedModel = string.IsNullOrWhiteSpace(model) ? "custom-model" : model.Trim();
        var providerId = "custom";
        var modelRef = $"{providerId}/{normalizedModel}";

        return new
        {
            agents = new
            {
                defaults = new
                {
                    workspace = NormalizeMountPath(workspaceMountPath),
                    model = new
                    {
                        primary = modelRef
                    },
                    models = new Dictionary<string, object>
                    {
                        [modelRef] = new
                        {
                            alias = normalizedModel
                        }
                    }
                }
            },
            models = new
            {
                mode = "merge",
                providers = new Dictionary<string, object>
                {
                    [providerId] = new
                    {
                        baseUrl = apiEndpoint.Trim(),
                        apiKey,
                        api = MapProviderApi(apiType),
                        models = new[]
                        {
                            new
                            {
                                id = normalizedModel,
                                name = normalizedModel
                            }
                        }
                    }
                }
            }
        };
    }

    private static string NormalizeApiType(string value)
    {
        return string.Equals(value?.Trim(), "messages", StringComparison.OrdinalIgnoreCase) ? "messages" : "chat";
    }

    private static string MapProviderApi(string apiType)
    {
        return string.Equals(apiType, "messages", StringComparison.OrdinalIgnoreCase)
            ? "anthropic-messages"
            : "openai-completions";
    }

    private static string BuildPersistentDirectory(string rootPath, string userName)
    {
        var safeUserName = SanitizePathSegment(userName, "user");
        return BuildChildPath(Path.GetFullPath(rootPath), safeUserName);
    }

    private static string BuildChildPath(string rootPath, string childName)
    {
        var fullRoot = Path.GetFullPath(rootPath);
        var combined = Path.GetFullPath(Path.Combine(fullRoot, childName));
        if (!combined.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Resolved path escapes configured root path.");
        }

        return combined;
    }

    private static string NormalizeFileName(string? fileName, string fallback)
    {
        var normalized = Path.GetFileName(string.IsNullOrWhiteSpace(fileName) ? fallback : fileName.Trim());
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }

    private static string NormalizeMountPath(string? path)
    {
        var normalized = string.IsNullOrWhiteSpace(path) ? "/app/data" : path.Trim().Replace('\\', '/');
        if (!normalized.StartsWith('/'))
        {
            normalized = "/" + normalized;
        }

        normalized = normalized.TrimEnd('/');
        return string.IsNullOrWhiteSpace(normalized) ? "/" : normalized;
    }

    private static string CombineMountPath(string? directory, string fileName)
    {
        var normalizedDirectory = NormalizeMountPath(directory);
        return normalizedDirectory == "/" ? $"/{fileName}" : $"{normalizedDirectory}/{fileName}";
    }

    private static string SanitizePathSegment(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value.Trim())
        {
            builder.Append(char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' ? ch : '-');
        }

        var result = builder.ToString().Trim('-', '.', ' ');
        return string.IsNullOrWhiteSpace(result) ? fallback : result;
    }
}
