using Microsoft.Extensions.Options;
using OpenSandbox.Abstractions;
using OpenSandbox.Abstractions.Contracts;
using OpenSandbox.Abstractions.Services;
using OpenSandbox.Server.Contracts;
using OpenSandbox.Server.Options;
using OpenSandbox.Server.Proxy;

namespace OpenSandbox.Server.Services;

public sealed class OpenSandboxService(
    ISandboxRuntime runtime,
    ISandboxStore store,
    IOptions<OpenSandboxServerOptions> options,
    ILogger<OpenSandboxService> logger,
    SignedProxyUrlService signedProxyUrlService)
{
    private readonly OpenSandboxServerOptions _options = options.Value;

    public async Task<CreateSandboxResponse> CreateAsync(CreateSandboxRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Image?.Uri))
        {
            throw new InvalidOperationException("image.uri is required.");
        }

        var neverExpires = request.NeverExpires;
        int? timeoutSeconds = neverExpires ? null : request.Timeout > 0 ? request.Timeout : 600;
        var createdAt = DateTimeOffset.UtcNow;
        var record = new SandboxRecord
        {
            Id = Guid.NewGuid().ToString(),
            ContainerName = $"opensandbox-{Guid.NewGuid():N}",
            Image = request.Image.Uri,
            Entrypoint = request.Entrypoint?.Where(static x => !string.IsNullOrWhiteSpace(x)).ToList() ?? new List<string>(),
            Metadata = request.Metadata == null ? null : new Dictionary<string, string>(request.Metadata),
            Env = request.Env == null ? null : new Dictionary<string, string>(request.Env),
            Volumes = request.Volumes?.Select(MapVolume).ToList(),
            ResourceLimits = request.ResourceLimits == null
                ? null
                : new SandboxResourceLimits
                {
                    Cpu = request.ResourceLimits.Cpu,
                    Memory = request.ResourceLimits.Memory
                },
            TimeoutSeconds = timeoutSeconds,
            NeverExpires = neverExpires,
            CreatedAt = createdAt,
            ExpiresAt = timeoutSeconds == null ? null : createdAt.AddSeconds(timeoutSeconds.Value)
        };

        var containerId = await runtime.CreateAsync(record, cancellationToken);
        record.ContainerId = containerId;
        await RefreshRecordAsync(record, cancellationToken);
        await store.UpsertAsync(record, cancellationToken);

        return ToCreateResponse(record);
    }

    public async Task<ListSandboxesResponse> ListAsync(int page, int pageSize, IReadOnlyCollection<string> states, IReadOnlyDictionary<string, string> metadataFilters, CancellationToken cancellationToken)
    {
        await DeleteExpiredAsync(cancellationToken);
        var records = (await store.ListAsync(cancellationToken)).ToList();

        foreach (var record in records)
        {
            await RefreshRecordAsync(record, cancellationToken, save: true);
        }

        IEnumerable<SandboxRecord> query = records;
        if (states.Count > 0)
        {
            query = query.Where(x => states.Contains(x.LastKnownState ?? string.Empty, StringComparer.OrdinalIgnoreCase));
        }

        foreach (var filter in metadataFilters)
        {
            query = query.Where(x => x.Metadata != null
                                     && x.Metadata.TryGetValue(filter.Key, out var value)
                                     && string.Equals(value, filter.Value, StringComparison.OrdinalIgnoreCase));
        }

        var normalizedPage = Math.Max(page, 1);
        var normalizedPageSize = Math.Clamp(pageSize, 1, 200);
        var totalItems = query.Count();
        var items = query
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(ToInfoResponse)
            .ToList();

        return new ListSandboxesResponse
        {
            Items = items,
            Pagination = new PaginationInfo
            {
                Page = normalizedPage,
                PageSize = normalizedPageSize,
                TotalItems = totalItems,
                TotalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)normalizedPageSize),
                HasNextPage = normalizedPage * normalizedPageSize < totalItems
            }
        };
    }

    public async Task<SandboxInfoResponse?> GetAsync(string id, CancellationToken cancellationToken)
    {
        var record = await GetActiveRecordAsync(id, cancellationToken);
        if (record == null)
        {
            return null;
        }

        await RefreshRecordAsync(record, cancellationToken, save: true);
        return ToInfoResponse(record);
    }

    public async Task<SandboxUsageResponse?> GetUsageAsync(string id, CancellationToken cancellationToken)
    {
        var record = await GetActiveRecordAsync(id, cancellationToken);
        if (record == null)
        {
            return null;
        }

        await RefreshRecordAsync(record, cancellationToken, save: true);
        if (!string.Equals(record.LastKnownState, SandboxStateNames.Running, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var stats = await runtime.GetUsageAsync(record.ContainerName, cancellationToken);
        if (stats == null)
        {
            return null;
        }

        return new SandboxUsageResponse
        {
            CpuPercent = stats.CpuPercent,
            MemoryPercent = stats.MemoryPercent,
            CpuLimit = record.ResourceLimits?.Cpu,
            MemoryUsage = stats.MemoryUsage,
            MemoryLimit = record.ResourceLimits?.Memory ?? stats.MemoryLimit,
            CollectedAt = stats.CollectedAt
        };
    }

    public async Task<ExecuteCommandResponse?> ExecuteCommandAsync(string id, string command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            throw new InvalidOperationException("command is required.");
        }

        var record = await GetActiveRecordAsync(id, cancellationToken);
        if (record == null)
        {
            return null;
        }

        await RefreshRecordAsync(record, cancellationToken, save: true);
        if (!string.Equals(record.LastKnownState, SandboxStateNames.Running, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only running sandboxes support command execution.");
        }

        var result = await runtime.ExecuteAsync(record.ContainerName, command, cancellationToken);
        return result == null
            ? null
            : new ExecuteCommandResponse
            {
                ExitCode = result.ExitCode,
                StdOut = result.StdOut,
                StdErr = result.StdErr
            };
    }

    public async Task<string?> GetTerminalContainerNameAsync(string id, CancellationToken cancellationToken)
    {
        var record = await GetActiveRecordAsync(id, cancellationToken);
        if (record == null)
        {
            return null;
        }

        await RefreshRecordAsync(record, cancellationToken, save: true);
        return string.Equals(record.LastKnownState, SandboxStateNames.Running, StringComparison.OrdinalIgnoreCase)
            ? record.ContainerName
            : null;
    }

    public async Task<ListFilesResponse?> ListFilesAsync(string id, string path, CancellationToken cancellationToken)
    {
        var record = await GetActiveRecordAsync(id, cancellationToken);
        if (record == null)
        {
            return null;
        }

        await RefreshRecordAsync(record, cancellationToken, save: true);
        var entries = await runtime.ListFilesAsync(record.ContainerName, path, cancellationToken);
        return new ListFilesResponse
        {
            Path = string.IsNullOrWhiteSpace(path) ? "/" : path,
            Entries = entries.Select(entry => new FileEntryResponse
            {
                Name = entry.Name,
                Path = entry.Path,
                IsDirectory = entry.IsDirectory,
                SizeBytes = entry.SizeBytes,
                LastModifiedAt = entry.LastModifiedAt
            }).ToList()
        };
    }

    public async Task<ReadFileResponse?> ReadFileAsync(string id, string path, CancellationToken cancellationToken)
    {
        var record = await GetActiveRecordAsync(id, cancellationToken);
        if (record == null)
        {
            return null;
        }

        await RefreshRecordAsync(record, cancellationToken, save: true);
        var file = await runtime.ReadFileAsync(record.ContainerName, path, cancellationToken);
        if (file == null)
        {
            return null;
        }

        return new ReadFileResponse
        {
            Path = file.Path,
            FileName = file.FileName,
            ContentBase64 = Convert.ToBase64String(file.Content)
        };
    }

    public async Task<bool> WriteFileAsync(string id, WriteFileRequest request, CancellationToken cancellationToken)
    {
        var record = await GetActiveRecordAsync(id, cancellationToken);
        if (record == null)
        {
            return false;
        }

        await RefreshRecordAsync(record, cancellationToken, save: true);
        var content = string.IsNullOrWhiteSpace(request.ContentBase64) ? [] : Convert.FromBase64String(request.ContentBase64);
        await runtime.WriteFileAsync(record.ContainerName, request.Path, content, cancellationToken);
        return true;
    }

    public async Task<bool> CreateDirectoryAsync(string id, CreateDirectoryRequest request, CancellationToken cancellationToken)
    {
        var record = await GetActiveRecordAsync(id, cancellationToken);
        if (record == null)
        {
            return false;
        }

        await RefreshRecordAsync(record, cancellationToken, save: true);
        await runtime.CreateDirectoryAsync(record.ContainerName, request.Path, cancellationToken);
        return true;
    }

    public async Task<bool> DeletePathAsync(string id, string path, bool recursive, CancellationToken cancellationToken)
    {
        var record = await GetActiveRecordAsync(id, cancellationToken);
        if (record == null)
        {
            return false;
        }

        await RefreshRecordAsync(record, cancellationToken, save: true);
        await runtime.DeletePathAsync(record.ContainerName, path, recursive, cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        var record = await store.GetAsync(id, cancellationToken);
        if (record == null)
        {
            return false;
        }

        await runtime.DeleteAsync(record.ContainerName, cancellationToken);
        await store.RemoveAsync(id, cancellationToken);
        return true;
    }

    public async Task<SandboxInfoResponse?> PauseAsync(string id, CancellationToken cancellationToken)
    {
        var record = await GetActiveRecordAsync(id, cancellationToken);
        if (record == null)
        {
            return null;
        }

        await runtime.PauseAsync(record.ContainerName, cancellationToken);
        await RefreshRecordAsync(record, cancellationToken, save: true);
        return ToInfoResponse(record);
    }

    public async Task<SandboxInfoResponse?> ResumeAsync(string id, CancellationToken cancellationToken)
    {
        var record = await GetActiveRecordAsync(id, cancellationToken);
        if (record == null)
        {
            return null;
        }

        await runtime.ResumeAsync(record.ContainerName, cancellationToken);
        await RefreshRecordAsync(record, cancellationToken, save: true);
        return ToInfoResponse(record);
    }

    public async Task<SandboxInfoResponse?> RestartAsync(string id, CancellationToken cancellationToken)
    {
        var record = await GetActiveRecordAsync(id, cancellationToken);
        if (record == null)
        {
            return null;
        }

        await runtime.RestartAsync(record.ContainerName, cancellationToken);
        await RefreshRecordAsync(record, cancellationToken, save: true);
        return ToInfoResponse(record);
    }

    public async Task<RenewSandboxExpirationResponse?> RenewAsync(string id, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        var record = await GetActiveRecordAsync(id, cancellationToken);
        if (record == null)
        {
            return null;
        }

        if (record.NeverExpires)
        {
            return new RenewSandboxExpirationResponse
            {
                ExpiresAt = null,
                NeverExpires = true
            };
        }

        if (expiresAt <= DateTimeOffset.UtcNow)
        {
            throw new InvalidOperationException("expiresAt must be later than now.");
        }

        record.ExpiresAt = expiresAt;
        await store.UpsertAsync(record, cancellationToken);
        return new RenewSandboxExpirationResponse
        {
            ExpiresAt = record.ExpiresAt,
            NeverExpires = false
        };
    }

    public async Task<EndpointResponse?> GetEndpointAsync(string id, int port, bool useServerProxy, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var record = await GetActiveRecordAsync(id, cancellationToken);
        if (record == null)
        {
            return null;
        }

        await RefreshRecordAsync(record, cancellationToken, save: true);
        if (!string.Equals(record.LastKnownState, SandboxStateNames.Running, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(record.LastKnownState, SandboxStateNames.Paused, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (useServerProxy)
        {
            var access = signedProxyUrlService.CreateAccess(httpContext, record.Id, port);
            return new EndpointResponse
            {
                EndpointAddress = $"{httpContext.Request.Host}{access.RelativePathAndQuery}",
                Url = access.AbsoluteUrl,
                Headers = BuildAuthHeaders(httpContext),
                ExpiresAt = access.ExpiresAt
            };
        }

        var publishedPort = await runtime.GetPublishedPortAsync(record.ContainerName, port, cancellationToken);
        if (publishedPort == null)
        {
            return null;
        }

        var host = string.IsNullOrWhiteSpace(_options.EndpointHost)
            ? httpContext.Request.Host.Host
            : _options.EndpointHost;
        var endpointAddress = $"{host}:{publishedPort.Value}";
        return new EndpointResponse
        {
            EndpointAddress = endpointAddress,
            Url = $"{httpContext.Request.Scheme}://{endpointAddress}",
            Headers = BuildAuthHeaders(httpContext)
        };
    }

    public async Task<string?> ResolveProxyDestinationAsync(string id, int port, CancellationToken cancellationToken)
    {
        var record = await GetActiveRecordAsync(id, cancellationToken);
        if (record == null)
        {
            return null;
        }

        await RefreshRecordAsync(record, cancellationToken, save: true);
        if (!string.Equals(record.LastKnownState, SandboxStateNames.Running, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var publishedPort = await runtime.GetPublishedPortAsync(record.ContainerName, port, cancellationToken);
        return publishedPort == null ? null : $"http://{_options.ProxyUpstreamHost}:{publishedPort.Value}";
    }

    public async Task<SandboxLogsResponse?> GetLogsAsync(string id, int tail, CancellationToken cancellationToken)
    {
        var record = await GetActiveRecordAsync(id, cancellationToken);
        if (record == null)
        {
            return null;
        }

        await RefreshRecordAsync(record, cancellationToken, save: true);
        var lines = await runtime.GetLogsAsync(record.ContainerName, tail, cancellationToken);
        return lines == null ? null : new SandboxLogsResponse { Lines = lines.ToList() };
    }

    public async Task<string?> GetLogsContainerNameAsync(string id, CancellationToken cancellationToken)
    {
        var record = await GetActiveRecordAsync(id, cancellationToken);
        if (record == null)
        {
            return null;
        }

        await RefreshRecordAsync(record, cancellationToken, save: true);
        return record.ContainerName;
    }

    public async Task DeleteExpiredAsync(CancellationToken cancellationToken)
    {
        var records = await store.ListAsync(cancellationToken);
        var expired = records.Where(static x => !x.NeverExpires && x.ExpiresAt.HasValue && x.ExpiresAt.Value <= DateTimeOffset.UtcNow).ToList();
        foreach (var item in expired)
        {
            try
            {
                await runtime.DeleteAsync(item.ContainerName, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to delete expired sandbox {SandboxId}.", item.Id);
            }

            await store.RemoveAsync(item.Id, cancellationToken);
        }
    }

    private async Task<SandboxRecord?> GetActiveRecordAsync(string id, CancellationToken cancellationToken)
    {
        var record = await store.GetAsync(id, cancellationToken);
        if (record == null)
        {
            return null;
        }

        if (!record.NeverExpires && record.ExpiresAt.HasValue && record.ExpiresAt.Value <= DateTimeOffset.UtcNow)
        {
            await DeleteAsync(id, cancellationToken);
            return null;
        }

        return record;
    }

    private async Task RefreshRecordAsync(SandboxRecord record, CancellationToken cancellationToken, bool save = false)
    {
        var state = await runtime.InspectAsync(record.ContainerName, cancellationToken);
        if (state == null)
        {
            record.LastKnownState = SandboxStateNames.Deleted;
            record.LastKnownReason = SandboxStateNames.Deleted;
            record.LastKnownMessage = "Container not found";
        }
        else
        {
            record.ContainerId = state.ContainerId;
            record.LastKnownState = state.State;
            record.LastKnownReason = state.Reason;
            record.LastKnownMessage = state.Message;
        }

        if (save)
        {
            await store.UpsertAsync(record, cancellationToken);
        }
    }

    private static Dictionary<string, string>? BuildAuthHeaders(HttpContext httpContext)
    {
        if (!httpContext.Items.TryGetValue("OpenSandbox.AuthHeaderName", out var headerName)
            || !httpContext.Items.TryGetValue("OpenSandbox.AuthHeaderValue", out var headerValue))
        {
            return null;
        }

        var name = headerName?.ToString();
        var value = headerValue?.ToString();
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return new Dictionary<string, string>
        {
            [name] = value
        };
    }

    private static SandboxVolumeSpec MapVolume(VolumeSpec volume)
    {
        return new SandboxVolumeSpec
        {
            Name = volume.Name,
            MountPath = volume.MountPath,
            ReadOnly = volume.ReadOnly,
            SubPath = volume.SubPath,
            Host = volume.Host == null ? null : new SandboxHostVolume
            {
                Path = volume.Host.Path
            }
        };
    }

    private static CreateSandboxResponse ToCreateResponse(SandboxRecord record)
    {
        return new CreateSandboxResponse
        {
            Id = record.Id,
            Status = ToStatus(record),
            ContainerId = record.ContainerId,
            Metadata = record.Metadata == null ? null : new Dictionary<string, string>(record.Metadata),
            CreatedAt = record.CreatedAt,
            ExpiresAt = record.ExpiresAt,
            NeverExpires = record.NeverExpires,
            Entrypoint = record.Entrypoint.ToList()
        };
    }

    private static SandboxInfoResponse ToInfoResponse(SandboxRecord record)
    {
        return new SandboxInfoResponse
        {
            Id = record.Id,
            ContainerId = record.ContainerId,
            Image = new ImageSpec { Uri = record.Image },
            Entrypoint = record.Entrypoint.ToList(),
            Metadata = record.Metadata == null ? null : new Dictionary<string, string>(record.Metadata),
            Status = ToStatus(record),
            CreatedAt = record.CreatedAt,
            ExpiresAt = record.ExpiresAt,
            NeverExpires = record.NeverExpires
        };
    }

    private static SandboxStatus ToStatus(SandboxRecord record)
    {
        return new SandboxStatus
        {
            State = record.LastKnownState ?? SandboxStateNames.Creating,
            Reason = record.LastKnownReason,
            Message = record.LastKnownMessage
        };
    }
}
