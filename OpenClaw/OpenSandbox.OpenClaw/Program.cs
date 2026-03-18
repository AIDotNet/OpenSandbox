using System.Net.WebSockets;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.FileProviders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenSandbox.OpenClaw.Contracts;
using OpenSandbox.OpenClaw.Data;
using OpenSandbox.OpenClaw.Domain;
using OpenSandbox.OpenClaw.Options;
using OpenSandbox.OpenClaw.Services;
using OpenSandbox.Server.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<OpenClawOptions>(builder.Configuration.GetSection(OpenClawOptions.SectionName));
builder.Services.Configure<AdminBootstrapOptions>(builder.Configuration.GetSection(AdminBootstrapOptions.SectionName));

var configuredDatabasePath = builder.Configuration.GetValue<string>($"{OpenClawOptions.SectionName}:DatabasePath") ?? "data/openclaw.db";
var databasePath = Path.IsPathRooted(configuredDatabasePath)
    ? configuredDatabasePath
    : Path.Combine(builder.Environment.ContentRootPath, configuredDatabasePath);
var databaseDirectory = Path.GetDirectoryName(databasePath)!;
Directory.CreateDirectory(databaseDirectory);
var dataProtectionPath = Path.Combine(databaseDirectory, "keys");
Directory.CreateDirectory(dataProtectionPath);

builder.Services.AddSingleton(databasePath);
builder.Services.AddDbContext<OpenClawDbContext>((sp, options) =>
{
    var resolvedDatabasePath = sp.GetRequiredService<string>();
    options.UseSqlite($"Data Source={resolvedDatabasePath}");
});
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath));
builder.Services.AddHttpClient(nameof(OpenSandboxGateway));
builder.Services.AddScoped<PasswordService>();
builder.Services.AddScoped<SecretProtector>();
builder.Services.AddScoped<OpenSandboxGateway>();
builder.Services.AddScoped<DeploymentService>();
builder.Services.AddScoped<AdminBootstrapper>();
builder.Services.AddHostedService<SandboxServerHealthBackgroundService>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = builder.Configuration[$"{OpenClawOptions.SectionName}:CookieName"] ?? "openclaw.session";
        options.LoginPath = "/login";
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api") || context.Request.Path.StartsWithSegments("/ws"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole(UserRole.Admin.ToString()));
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());
});

var app = builder.Build();

var webDistPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "web", "dist"));
if (Directory.Exists(webDistPath))
{
    var fileProvider = new PhysicalFileProvider(webDistPath);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fileProvider });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = fileProvider });
}

using (var scope = app.Services.CreateScope())
{
    var bootstrapper = scope.ServiceProvider.GetRequiredService<AdminBootstrapper>();
    await bootstrapper.InitializeAsync(CancellationToken.None);
}

app.UseWebSockets();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseSwagger();
app.UseSwaggerUI();

var api = app.MapGroup("/api");

api.MapGet("/meta", () => Results.Ok(new
{
    application = "OpenClaw",
    version = GetApplicationVersion()
}));

api.MapPost("/auth/login", async (LoginRequest request, OpenClawDbContext dbContext, PasswordService passwordService, HttpContext httpContext, CancellationToken cancellationToken) =>
{
    var user = await dbContext.Users.FirstOrDefaultAsync(x => x.UserName == request.UserName, cancellationToken);
    if (user == null || user.Status != UserStatus.Active || !passwordService.VerifyPassword(user, request.Password))
    {
        return Results.Unauthorized();
    }

    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new(ClaimTypes.Name, user.UserName),
        new(ClaimTypes.GivenName, user.DisplayName),
        new(ClaimTypes.Role, user.Role.ToString())
    };
    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    return Results.Ok(new CurrentUserResponse(user.Id, user.UserName, user.DisplayName, user.Role.ToString()));
});

api.MapPost("/auth/logout", async (HttpContext httpContext) =>
{
    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Ok();
}).RequireAuthorization();

api.MapGet("/auth/me", async (ClaimsPrincipal principal, OpenClawDbContext dbContext, CancellationToken cancellationToken) =>
{
    var user = await GetCurrentUserAsync(principal, dbContext, cancellationToken);
    return user == null ? Results.Unauthorized() : Results.Ok(new CurrentUserResponse(user.Id, user.UserName, user.DisplayName, user.Role.ToString()));
}).RequireAuthorization();

var admin = api.MapGroup("/admin").RequireAuthorization("AdminOnly");

admin.MapGet("/users", async (OpenClawDbContext dbContext, CancellationToken cancellationToken) =>
    Results.Ok(await dbContext.Users.OrderBy(x => x.UserName).Select(x => new { x.Id, x.UserName, x.DisplayName, Role = x.Role.ToString(), Status = x.Status.ToString(), x.CreatedAt }).ToListAsync(cancellationToken)));

admin.MapPost("/users", async (CreateUserRequest request, OpenClawDbContext dbContext, PasswordService passwordService, CancellationToken cancellationToken) =>
{
    if (await dbContext.Users.AnyAsync(x => x.UserName == request.UserName, cancellationToken))
    {
        return Results.BadRequest(new { message = "用户名已存在" });
    }

    var role = Enum.TryParse<UserRole>(request.Role, true, out var parsedRole) ? parsedRole : UserRole.Employee;
    var user = new AppUser { UserName = request.UserName.Trim(), DisplayName = request.DisplayName.Trim(), Role = role };
    user.PasswordHash = passwordService.HashPassword(user, request.Password);
    dbContext.Users.Add(user);
    await dbContext.SaveChangesAsync(cancellationToken);
    return Results.Ok(new { user.Id });
});

admin.MapPut("/users/{id:guid}", async (Guid id, UpdateUserRequest request, OpenClawDbContext dbContext, CancellationToken cancellationToken) =>
{
    var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    if (user == null)
    {
        return Results.NotFound();
    }

    var nextRole = Enum.TryParse<UserRole>(request.Role, true, out var parsedRole) ? parsedRole : user.Role;
    var nextStatus = Enum.TryParse<UserStatus>(request.Status, true, out var parsedStatus) ? parsedStatus : user.Status;
    if (user.Role == UserRole.Admin && (nextRole != UserRole.Admin || nextStatus != UserStatus.Active))
    {
        var activeAdminCount = await dbContext.Users.CountAsync(x => x.Role == UserRole.Admin && x.Status == UserStatus.Active, cancellationToken);
        if (activeAdminCount <= 1)
        {
            return Results.BadRequest(new { message = "至少保留一个启用中的管理员" });
        }
    }

    user.DisplayName = request.DisplayName.Trim();
    user.Role = nextRole;
    user.Status = nextStatus;
    await dbContext.SaveChangesAsync(cancellationToken);
    return Results.Ok();
});

admin.MapPost("/users/{id:guid}/password", async (Guid id, ResetPasswordRequest request, OpenClawDbContext dbContext, PasswordService passwordService, CancellationToken cancellationToken) =>
{
    var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    if (user == null)
    {
        return Results.NotFound();
    }

    user.PasswordHash = passwordService.HashPassword(user, request.Password);
    await dbContext.SaveChangesAsync(cancellationToken);
    return Results.Ok();
});

admin.MapDelete("/users/{id:guid}", async (Guid id, ClaimsPrincipal principal, OpenClawDbContext dbContext, CancellationToken cancellationToken) =>
{
    var currentUser = await GetCurrentUserAsync(principal, dbContext, cancellationToken);
    var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    if (user == null)
    {
        return Results.NotFound();
    }

    if (currentUser?.Id == user.Id)
    {
        return Results.BadRequest(new { message = "不能删除当前登录账号" });
    }

    if (user.Role == UserRole.Admin)
    {
        var adminCount = await dbContext.Users.CountAsync(x => x.Role == UserRole.Admin && x.Status == UserStatus.Active, cancellationToken);
        if (adminCount <= 1)
        {
            return Results.BadRequest(new { message = "至少保留一个启用中的管理员" });
        }
    }

    var hasDeployments = await dbContext.DeploymentInstances.AnyAsync(x => x.UserId == id, cancellationToken);
    if (hasDeployments)
    {
        return Results.BadRequest(new { message = "该用户仍有关联部署实例，不能删除" });
    }

    dbContext.Users.Remove(user);
    await dbContext.SaveChangesAsync(cancellationToken);
    return Results.NoContent();
});

admin.MapGet("/sandbox-servers", async (OpenClawDbContext dbContext, CancellationToken cancellationToken) =>
    Results.Ok(await dbContext.SandboxServers.OrderBy(x => x.Name).Select(x => new
    {
        x.Id,
        x.Name,
        x.BaseUrl,
        x.ApiToken,
        x.PersistentRootPath,
        x.IsEnabled,
        HealthStatus = x.HealthStatus.ToString(),
        x.LastCheckedAt,
        x.LastHealthMessage
    }).ToListAsync(cancellationToken)));

admin.MapPost("/sandbox-servers", async (SandboxServerRequest request, OpenClawDbContext dbContext, CancellationToken cancellationToken) =>
{
    var entity = new SandboxServerNode
    {
        Name = request.Name.Trim(),
        BaseUrl = request.BaseUrl.Trim().TrimEnd('/'),
        ApiToken = request.ApiToken.Trim(),
        PersistentRootPath = request.PersistentRootPath.Trim(),
        IsEnabled = request.IsEnabled
    };
    dbContext.SandboxServers.Add(entity);
    await dbContext.SaveChangesAsync(cancellationToken);
    return Results.Ok(entity);
});

admin.MapPut("/sandbox-servers/{id:guid}", async (Guid id, SandboxServerRequest request, OpenClawDbContext dbContext, CancellationToken cancellationToken) =>
{
    var entity = await dbContext.SandboxServers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    if (entity == null)
    {
        return Results.NotFound();
    }

    entity.Name = request.Name.Trim();
    entity.BaseUrl = request.BaseUrl.Trim().TrimEnd('/');
    entity.ApiToken = request.ApiToken.Trim();
    entity.PersistentRootPath = request.PersistentRootPath.Trim();
    entity.IsEnabled = request.IsEnabled;
    await dbContext.SaveChangesAsync(cancellationToken);
    return Results.Ok(entity);
});

admin.MapPost("/sandbox-servers/{id:guid}/health", async (Guid id, OpenClawDbContext dbContext, OpenSandboxGateway gateway, CancellationToken cancellationToken) =>
{
    var entity = await dbContext.SandboxServers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    if (entity == null)
    {
        return Results.NotFound();
    }

    try
    {
        var ok = await gateway.PingAsync(entity.BaseUrl, entity.ApiToken, cancellationToken);
        entity.HealthStatus = ok ? SandboxServerStatus.Healthy : SandboxServerStatus.Unhealthy;
        entity.LastHealthMessage = ok ? "OK" : "Ping failed";
    }
    catch (Exception ex)
    {
        entity.HealthStatus = SandboxServerStatus.Unhealthy;
        entity.LastHealthMessage = ex.Message;
    }

    entity.LastCheckedAt = DateTimeOffset.UtcNow;
    await dbContext.SaveChangesAsync(cancellationToken);
    return Results.Ok(entity);
});

admin.MapDelete("/sandbox-servers/{id:guid}", async (Guid id, OpenClawDbContext dbContext, CancellationToken cancellationToken) =>
{
    var entity = await dbContext.SandboxServers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    if (entity == null)
    {
        return Results.NotFound();
    }

    var inUse = await dbContext.DeploymentInstances.AnyAsync(x => x.SandboxServerId == id, cancellationToken);
    if (inUse)
    {
        return Results.BadRequest(new { message = "该沙盒服务端仍有关联部署实例，不能删除" });
    }

    dbContext.SandboxServers.Remove(entity);
    await dbContext.SaveChangesAsync(cancellationToken);
    return Results.NoContent();
});

admin.MapGet("/settings", async (OpenClawDbContext dbContext, CancellationToken cancellationToken) =>
    Results.Ok(await dbContext.SystemSettings.FirstAsync(cancellationToken)));

admin.MapPut("/settings", async (SystemSettingsRequest request, OpenClawDbContext dbContext, CancellationToken cancellationToken) =>
{
    var settings = await dbContext.SystemSettings.FirstAsync(cancellationToken);
    settings.DefaultCpu = request.DefaultCpu.Trim();
    settings.DefaultMemory = request.DefaultMemory.Trim();
    settings.DefaultLogTailLines = Math.Clamp(request.DefaultLogTailLines, 10, 2000);
    await dbContext.SaveChangesAsync(cancellationToken);
    return Results.Ok(settings);
});

admin.MapGet("/templates", async (OpenClawDbContext dbContext, CancellationToken cancellationToken) =>
{
    var templates = await dbContext.Templates
        .Include(x => x.Versions)
        .OrderBy(x => x.Name)
        .ToListAsync(cancellationToken);

    return Results.Ok(templates.Select(x => new
    {
        x.Id,
        x.Name,
        x.Description,
        x.IsBuiltin,
        x.IsEnabled,
        x.CurrentVersionId,
        Versions = x.Versions
            .OrderByDescending(v => v.CreatedAt)
            .Select(v => new
            {
                v.Id,
                v.Version,
                v.Image,
                v.ContainerPort,
                v.ConfigMountPath,
                v.ConfigFileName,
                v.WorkspaceMountPath,
                v.IsActive,
                v.CreatedAt,
                Command = System.Text.Json.JsonSerializer.Deserialize<List<string>>(v.CommandJson) ?? new List<string>()
            })
            .ToList()
    }));
});

admin.MapPost("/templates", async (TemplateRequest request, OpenClawDbContext dbContext, CancellationToken cancellationToken) =>
{
    var template = new DeploymentTemplate { Name = request.Name.Trim(), Description = request.Description.Trim(), IsEnabled = request.IsEnabled };
    dbContext.Templates.Add(template);
    await dbContext.SaveChangesAsync(cancellationToken);
    return Results.Ok(template);
});

admin.MapPost("/templates/{id:guid}/versions", async (Guid id, TemplateVersionRequest request, OpenClawDbContext dbContext, CancellationToken cancellationToken) =>
{
    var template = await dbContext.Templates.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    if (template == null)
    {
        return Results.NotFound();
    }

    if (request.IsActive)
    {
        var oldVersions = await dbContext.TemplateVersions.Where(x => x.TemplateId == id && x.IsActive).ToListAsync(cancellationToken);
        foreach (var oldVersion in oldVersions)
        {
            oldVersion.IsActive = false;
        }
    }

    var version = new DeploymentTemplateVersion
    {
        TemplateId = id,
        Version = request.Version,
        Image = request.Image,
        ContainerPort = request.ContainerPort,
        CommandJson = System.Text.Json.JsonSerializer.Serialize(request.Command),
        ConfigMountPath = request.ConfigMountPath,
        ConfigFileName = request.ConfigFileName,
        WorkspaceMountPath = request.WorkspaceMountPath,
        IsActive = request.IsActive
    };
    dbContext.TemplateVersions.Add(version);
    template.CurrentVersionId = version.Id;
    await dbContext.SaveChangesAsync(cancellationToken);
    return Results.Ok(version);
});

admin.MapDelete("/templates/{id:guid}", async (Guid id, OpenClawDbContext dbContext, CancellationToken cancellationToken) =>
{
    var template = await dbContext.Templates.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    if (template == null)
    {
        return Results.NotFound();
    }

    var inUse = await dbContext.DeploymentInstances.AnyAsync(x => x.TemplateId == id, cancellationToken);
    if (inUse)
    {
        return Results.BadRequest(new { message = "该模板仍有关联部署实例，不能删除" });
    }

    dbContext.Templates.Remove(template);
    await dbContext.SaveChangesAsync(cancellationToken);
    return Results.NoContent();
});

api.MapGet("/sandbox-servers", async (OpenClawDbContext dbContext, CancellationToken cancellationToken) =>
    Results.Ok(await dbContext.SandboxServers.Where(x => x.IsEnabled).OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, HealthStatus = x.HealthStatus.ToString(), x.LastCheckedAt, x.LastHealthMessage }).ToListAsync(cancellationToken))
).RequireAuthorization();

api.MapGet("/templates", async (OpenClawDbContext dbContext, CancellationToken cancellationToken) =>
    Results.Ok(await dbContext.Templates.Where(x => x.IsEnabled).Select(x => new { x.Id, x.Name, x.Description, x.CurrentVersionId }).ToListAsync(cancellationToken))
).RequireAuthorization();

api.MapGet("/deployments", async (ClaimsPrincipal principal, OpenClawDbContext dbContext, CancellationToken cancellationToken) =>
{
    var user = await GetCurrentUserAsync(principal, dbContext, cancellationToken);
    if (user == null)
    {
        return Results.Unauthorized();
    }

    var query = dbContext.DeploymentInstances.Include(x => x.SandboxServer).Include(x => x.User).AsQueryable();
    if (user.Role != UserRole.Admin)
    {
        query = query.Where(x => x.UserId == user.Id);
    }

    var items = await query.ToListAsync(cancellationToken);
    return Results.Ok(items
        .OrderByDescending(x => x.UpdatedAt)
        .Select(x => new
    {
        x.Id,
        x.SandboxId,
        x.ContainerId,
        x.ApiEndpoint,
        x.ApiType,
        x.Model,
        x.CreatedAt,
        x.UpdatedAt,
        ServerName = x.SandboxServer!.Name,
        UserName = x.User!.UserName
    }));
}).RequireAuthorization();

api.MapPost("/deployments", async (ClaimsPrincipal principal, DeployRequest request, OpenClawDbContext dbContext, DeploymentService deploymentService, CancellationToken cancellationToken) =>
{
    var user = await GetCurrentUserAsync(principal, dbContext, cancellationToken);
    if (user == null)
    {
        return Results.Unauthorized();
    }

    return Results.Ok(await deploymentService.UpsertDeploymentAsync(user.Id, request, cancellationToken));
}).RequireAuthorization();

api.MapGet("/deployments/{id:guid}", async (Guid id, ClaimsPrincipal principal, OpenClawDbContext dbContext, DeploymentService deploymentService, CancellationToken cancellationToken) =>
{
    var user = await GetCurrentUserAsync(principal, dbContext, cancellationToken);
    if (user == null)
    {
        return Results.Unauthorized();
    }

    var instance = await dbContext.DeploymentInstances.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    if (instance == null || !CanAccess(user, instance))
    {
        return Results.NotFound();
    }

    var detail = await deploymentService.GetDeploymentDetailAsync(id, cancellationToken);
    return detail == null ? Results.NotFound() : Results.Ok(detail);
}).RequireAuthorization();

api.MapGet("/containers/{containerId}", async (string containerId, ClaimsPrincipal principal, OpenClawDbContext dbContext, DeploymentService deploymentService, CancellationToken cancellationToken) =>
{
    var user = await GetCurrentUserAsync(principal, dbContext, cancellationToken);
    if (user == null)
    {
        return Results.Unauthorized();
    }

    var instance = await dbContext.DeploymentInstances.FirstOrDefaultAsync(x => x.ContainerId == containerId, cancellationToken);
    if (instance == null || !CanAccess(user, instance))
    {
        return Results.NotFound();
    }

    var detail = await deploymentService.GetDeploymentDetailAsync(instance.Id, cancellationToken);
    return detail == null ? Results.NotFound() : Results.Ok(detail);
}).RequireAuthorization();

api.MapDelete("/deployments/{id:guid}", async (Guid id, ClaimsPrincipal principal, OpenClawDbContext dbContext, DeploymentService deploymentService, CancellationToken cancellationToken) =>
{
    var user = await GetCurrentUserAsync(principal, dbContext, cancellationToken);
    if (user == null)
    {
        return Results.Unauthorized();
    }

    var instance = await dbContext.DeploymentInstances.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    if (instance == null || !CanAccess(user, instance))
    {
        return Results.NotFound();
    }

    var deleted = await deploymentService.DeleteDeploymentAsync(id, cancellationToken);
    return deleted ? Results.NoContent() : Results.NotFound();
}).RequireAuthorization();

api.MapGet("/deployments/{id:guid}/logs", async (Guid id, ClaimsPrincipal principal, OpenClawDbContext dbContext, OpenSandboxGateway gateway, CancellationToken cancellationToken, int? tail) =>
{
    var user = await GetCurrentUserAsync(principal, dbContext, cancellationToken);
    if (user == null)
    {
        return Results.Unauthorized();
    }

    var instance = await dbContext.DeploymentInstances.Include(x => x.SandboxServer).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    if (instance == null || instance.SandboxServer == null || !CanAccess(user, instance) || string.IsNullOrWhiteSpace(instance.SandboxId))
    {
        return Results.NotFound();
    }

    var settings = await dbContext.SystemSettings.FirstAsync(cancellationToken);
    var result = await gateway.GetLogsAsync(instance.SandboxServer.BaseUrl, instance.SandboxServer.ApiToken, instance.SandboxId, tail ?? settings.DefaultLogTailLines, cancellationToken);
    return result == null ? Results.NotFound() : Results.Ok(result);
}).RequireAuthorization();

api.MapGet("/containers/{containerId}/logs", async (string containerId, ClaimsPrincipal principal, OpenClawDbContext dbContext, OpenSandboxGateway gateway, CancellationToken cancellationToken, int? tail) =>
{
    var user = await GetCurrentUserAsync(principal, dbContext, cancellationToken);
    if (user == null)
    {
        return Results.Unauthorized();
    }

    var instance = await dbContext.DeploymentInstances.Include(x => x.SandboxServer).FirstOrDefaultAsync(x => x.ContainerId == containerId, cancellationToken);
    if (instance == null || instance.SandboxServer == null || !CanAccess(user, instance) || string.IsNullOrWhiteSpace(instance.SandboxId))
    {
        return Results.NotFound();
    }

    var settings = await dbContext.SystemSettings.FirstAsync(cancellationToken);
    var result = await gateway.GetLogsAsync(instance.SandboxServer.BaseUrl, instance.SandboxServer.ApiToken, instance.SandboxId, tail ?? settings.DefaultLogTailLines, cancellationToken);
    return result == null ? Results.NotFound() : Results.Ok(result);
}).RequireAuthorization();

api.MapGet("/containers/{containerId}/files", async (string containerId, string? path, ClaimsPrincipal principal, OpenClawDbContext dbContext, OpenSandboxGateway gateway, CancellationToken cancellationToken) =>
{
    var user = await GetCurrentUserAsync(principal, dbContext, cancellationToken);
    if (user == null)
    {
        return Results.Unauthorized();
    }

    var instance = await dbContext.DeploymentInstances.Include(x => x.SandboxServer).FirstOrDefaultAsync(x => x.ContainerId == containerId, cancellationToken);
    if (instance == null || instance.SandboxServer == null || !CanAccess(user, instance) || string.IsNullOrWhiteSpace(instance.SandboxId))
    {
        return Results.NotFound();
    }

    try
    {
        var result = await gateway.ListFilesAsync(instance.SandboxServer.BaseUrl, instance.SandboxServer.ApiToken, instance.SandboxId, path ?? "/", cancellationToken);
        return result == null ? Results.NotFound() : Results.Ok(result);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway, title: "上游沙盒文件列表失败");
    }
}).RequireAuthorization();

api.MapGet("/containers/{containerId}/files/content", async (string containerId, string path, ClaimsPrincipal principal, OpenClawDbContext dbContext, OpenSandboxGateway gateway, CancellationToken cancellationToken) =>
{
    var user = await GetCurrentUserAsync(principal, dbContext, cancellationToken);
    if (user == null)
    {
        return Results.Unauthorized();
    }

    var instance = await dbContext.DeploymentInstances.Include(x => x.SandboxServer).FirstOrDefaultAsync(x => x.ContainerId == containerId, cancellationToken);
    if (instance == null || instance.SandboxServer == null || !CanAccess(user, instance) || string.IsNullOrWhiteSpace(instance.SandboxId))
    {
        return Results.NotFound();
    }

    try
    {
        var result = await gateway.ReadFileAsync(instance.SandboxServer.BaseUrl, instance.SandboxServer.ApiToken, instance.SandboxId, path, cancellationToken);
        return result == null ? Results.NotFound() : Results.Ok(result);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway, title: "上游沙盒文件读取失败");
    }
}).RequireAuthorization();

api.MapPost("/containers/{containerId}/files/content", async (string containerId, DeploymentWriteFileRequest request, ClaimsPrincipal principal, OpenClawDbContext dbContext, OpenSandboxGateway gateway, CancellationToken cancellationToken) =>
{
    var user = await GetCurrentUserAsync(principal, dbContext, cancellationToken);
    if (user == null)
    {
        return Results.Unauthorized();
    }

    var instance = await dbContext.DeploymentInstances.Include(x => x.SandboxServer).FirstOrDefaultAsync(x => x.ContainerId == containerId, cancellationToken);
    if (instance == null || instance.SandboxServer == null || !CanAccess(user, instance) || string.IsNullOrWhiteSpace(instance.SandboxId))
    {
        return Results.NotFound();
    }

    try
    {
        var ok = await gateway.WriteFileAsync(instance.SandboxServer.BaseUrl, instance.SandboxServer.ApiToken, instance.SandboxId, new WriteFileRequest { Path = request.Path, ContentBase64 = request.ContentBase64 }, cancellationToken);
        return ok ? Results.Ok() : Results.NotFound();
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway, title: "上游沙盒文件保存失败");
    }
}).RequireAuthorization();

api.MapPost("/containers/{containerId}/directories", async (string containerId, DeploymentCreateDirectoryRequest request, ClaimsPrincipal principal, OpenClawDbContext dbContext, OpenSandboxGateway gateway, CancellationToken cancellationToken) =>
{
    var user = await GetCurrentUserAsync(principal, dbContext, cancellationToken);
    if (user == null)
    {
        return Results.Unauthorized();
    }

    var instance = await dbContext.DeploymentInstances.Include(x => x.SandboxServer).FirstOrDefaultAsync(x => x.ContainerId == containerId, cancellationToken);
    if (instance == null || instance.SandboxServer == null || !CanAccess(user, instance) || string.IsNullOrWhiteSpace(instance.SandboxId))
    {
        return Results.NotFound();
    }

    try
    {
        var ok = await gateway.CreateDirectoryAsync(instance.SandboxServer.BaseUrl, instance.SandboxServer.ApiToken, instance.SandboxId, new CreateDirectoryRequest { Path = request.Path }, cancellationToken);
        return ok ? Results.Ok() : Results.NotFound();
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway, title: "上游沙盒目录创建失败");
    }
}).RequireAuthorization();

api.MapDelete("/containers/{containerId}/files", async (string containerId, string path, bool recursive, ClaimsPrincipal principal, OpenClawDbContext dbContext, OpenSandboxGateway gateway, CancellationToken cancellationToken) =>
{
    var user = await GetCurrentUserAsync(principal, dbContext, cancellationToken);
    if (user == null)
    {
        return Results.Unauthorized();
    }

    var instance = await dbContext.DeploymentInstances.Include(x => x.SandboxServer).FirstOrDefaultAsync(x => x.ContainerId == containerId, cancellationToken);
    if (instance == null || instance.SandboxServer == null || !CanAccess(user, instance) || string.IsNullOrWhiteSpace(instance.SandboxId))
    {
        return Results.NotFound();
    }

    try
    {
        var ok = await gateway.DeletePathAsync(instance.SandboxServer.BaseUrl, instance.SandboxServer.ApiToken, instance.SandboxId, path, recursive, cancellationToken);
        return ok ? Results.NoContent() : Results.NotFound();
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway, title: "上游沙盒路径删除失败");
    }
}).RequireAuthorization();

app.Map("/api/deployments/{id:guid}/terminal/ws", async (Guid id, HttpContext httpContext, OpenClawDbContext dbContext, OpenSandboxGateway gateway, CancellationToken cancellationToken) =>
{
    if (!httpContext.WebSockets.IsWebSocketRequest)
    {
        return Results.BadRequest();
    }

    var user = await GetCurrentUserAsync(httpContext.User, dbContext, cancellationToken);
    if (user == null)
    {
        return Results.Unauthorized();
    }

    var instance = await dbContext.DeploymentInstances.Include(x => x.SandboxServer).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    if (instance == null || instance.SandboxServer == null || !CanAccess(user, instance) || string.IsNullOrWhiteSpace(instance.SandboxId))
    {
        return Results.NotFound();
    }

    return await BridgeSandboxWebSocketAsync(
        httpContext,
        gateway,
        instance.SandboxServer.BaseUrl,
        instance.SandboxId,
        instance.SandboxServer.ApiToken,
        "terminal",
        cancellationToken);
}).RequireAuthorization();

app.Map("/api/deployments/{id:guid}/logs/ws", async (Guid id, HttpContext httpContext, OpenClawDbContext dbContext, OpenSandboxGateway gateway, CancellationToken cancellationToken) =>
{
    if (!httpContext.WebSockets.IsWebSocketRequest)
    {
        return Results.BadRequest();
    }

    var user = await GetCurrentUserAsync(httpContext.User, dbContext, cancellationToken);
    if (user == null)
    {
        return Results.Unauthorized();
    }

    var instance = await dbContext.DeploymentInstances.Include(x => x.SandboxServer).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    if (instance == null || instance.SandboxServer == null || !CanAccess(user, instance) || string.IsNullOrWhiteSpace(instance.SandboxId))
    {
        return Results.NotFound();
    }

    return await BridgeSandboxWebSocketAsync(
        httpContext,
        gateway,
        instance.SandboxServer.BaseUrl,
        instance.SandboxId,
        instance.SandboxServer.ApiToken,
        "logs",
        cancellationToken);
}).RequireAuthorization();

app.Map("/api/containers/{containerId}/terminal/ws", async (string containerId, HttpContext httpContext, OpenClawDbContext dbContext, OpenSandboxGateway gateway, CancellationToken cancellationToken) =>
{
    if (!httpContext.WebSockets.IsWebSocketRequest)
    {
        return Results.BadRequest();
    }

    var user = await GetCurrentUserAsync(httpContext.User, dbContext, cancellationToken);
    if (user == null)
    {
        return Results.Unauthorized();
    }

    var instance = await dbContext.DeploymentInstances.Include(x => x.SandboxServer).FirstOrDefaultAsync(x => x.ContainerId == containerId, cancellationToken);
    if (instance == null || instance.SandboxServer == null || !CanAccess(user, instance) || string.IsNullOrWhiteSpace(instance.SandboxId))
    {
        return Results.NotFound();
    }

    return await BridgeSandboxWebSocketAsync(
        httpContext,
        gateway,
        instance.SandboxServer.BaseUrl,
        instance.SandboxId,
        instance.SandboxServer.ApiToken,
        "terminal",
        cancellationToken);
}).RequireAuthorization();

app.Map("/api/containers/{containerId}/logs/ws", async (string containerId, HttpContext httpContext, OpenClawDbContext dbContext, OpenSandboxGateway gateway, CancellationToken cancellationToken) =>
{
    if (!httpContext.WebSockets.IsWebSocketRequest)
    {
        return Results.BadRequest();
    }

    var user = await GetCurrentUserAsync(httpContext.User, dbContext, cancellationToken);
    if (user == null)
    {
        return Results.Unauthorized();
    }

    var instance = await dbContext.DeploymentInstances.Include(x => x.SandboxServer).FirstOrDefaultAsync(x => x.ContainerId == containerId, cancellationToken);
    if (instance == null || instance.SandboxServer == null || !CanAccess(user, instance) || string.IsNullOrWhiteSpace(instance.SandboxId))
    {
        return Results.NotFound();
    }

    return await BridgeSandboxWebSocketAsync(
        httpContext,
        gateway,
        instance.SandboxServer.BaseUrl,
        instance.SandboxId,
        instance.SandboxServer.ApiToken,
        "logs",
        cancellationToken);
}).RequireAuthorization();

if (Directory.Exists(webDistPath))
{
    app.MapFallbackToFile("index.html", new StaticFileOptions { FileProvider = new PhysicalFileProvider(webDistPath) });
}

app.Run();
return;

static async Task<IResult> BridgeSandboxWebSocketAsync(HttpContext httpContext, OpenSandboxGateway gateway, string baseUrl, string sandboxId, string token, string streamType, CancellationToken cancellationToken)
{
    ClientWebSocket? upstream = null;
    try
    {
        var uri = BuildSandboxWebSocketUri(baseUrl, sandboxId, streamType);
        upstream = await gateway.ConnectWebSocketAsync(uri, token, cancellationToken);
        using var clientSocket = await httpContext.WebSockets.AcceptWebSocketAsync();
        await gateway.BridgeWebSocketAsync(upstream, clientSocket, cancellationToken);
        return Results.Empty;
    }
    catch (WebSocketException ex) when (ex.Message.Contains("status code '404'", StringComparison.Ordinal))
    {
        upstream?.Dispose();
        return Results.NotFound();
    }
    catch (WebSocketException ex)
    {
        upstream?.Dispose();
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway, title: "上游 WebSocket 握手失败");
    }
}

static Uri BuildSandboxWebSocketUri(string baseUrl, string sandboxId, string streamType)
{
    var normalizedBaseUrl = baseUrl.Trim().TrimEnd('/');
    var apiBaseUrl = normalizedBaseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
        ? normalizedBaseUrl
        : $"{normalizedBaseUrl}/v1";
    var uri = new Uri($"{apiBaseUrl}/sandboxes/{Uri.EscapeDataString(sandboxId)}/{streamType}/ws");
    var builder = new UriBuilder(uri)
    {
        Scheme = string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ? Uri.UriSchemeWss : Uri.UriSchemeWs
    };
    return builder.Uri;
}

static async Task<AppUser?> GetCurrentUserAsync(ClaimsPrincipal principal, OpenClawDbContext dbContext, CancellationToken cancellationToken)
{
    var idValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
    if (!Guid.TryParse(idValue, out var userId))
    {
        return null;
    }

    return await dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId && x.Status == UserStatus.Active, cancellationToken);
}

static bool CanAccess(AppUser user, DeploymentInstance instance)
{
    return user.Role == UserRole.Admin || instance.UserId == user.Id;
}

static string GetApplicationVersion()
{
    var assembly = typeof(PasswordService).Assembly;
    var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
    if (!string.IsNullOrWhiteSpace(informationalVersion))
    {
        return informationalVersion.Split('+')[0];
    }

    return assembly.GetName().Version?.ToString() ?? "dev";
}
