using Serilog;
using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using Microsoft.Extensions.DependencyInjection;

// Parse optional port from command-line or environment and set ASPNETCORE_URLS before building the host
// Supported: --port=5080, --port 5080, --aspnetcore-port=5080, -p 5080, port=5080
var _cmdArgs = Environment.GetCommandLineArgs();
int? _parsedPort = null;
for (int _i = 0; _i < _cmdArgs.Length; _i++)
{
    var _a = _cmdArgs[_i] ?? string.Empty;
    if (_a.StartsWith("--port=", StringComparison.OrdinalIgnoreCase))
    {
        if (int.TryParse(_a.Substring(7), out var _v)) { _parsedPort = _v; break; }
    }
    if (_a.Equals("--port", StringComparison.OrdinalIgnoreCase) && _i + 1 < _cmdArgs.Length)
    {
        if (int.TryParse(_cmdArgs[_i + 1], out var _v)) { _parsedPort = _v; break; }
    }
    if (_a.StartsWith("--aspnetcore-port=", StringComparison.OrdinalIgnoreCase))
    {
        if (int.TryParse(_a.Substring(18), out var _v)) { _parsedPort = _v; break; }
    }
    if (_a.StartsWith("-p=", StringComparison.OrdinalIgnoreCase))
    {
        if (int.TryParse(_a.Substring(3), out var _v)) { _parsedPort = _v; break; }
    }
    if (_a.Equals("-p", StringComparison.OrdinalIgnoreCase) && _i + 1 < _cmdArgs.Length)
    {
        if (int.TryParse(_cmdArgs[_i + 1], out var _v)) { _parsedPort = _v; break; }
    }
    // legacy style key=value
    if (_a.StartsWith("port=", StringComparison.OrdinalIgnoreCase))
    {
        if (int.TryParse(_a.Substring(5), out var _v)) { _parsedPort = _v; break; }
    }
}

// Check environment fallbacks if not present on command-line
if (!_parsedPort.HasValue)
{
    var _envPort = Environment.GetEnvironmentVariable("ASPNETCORE_PORT") ?? Environment.GetEnvironmentVariable("PORT");
    if (!string.IsNullOrWhiteSpace(_envPort) && int.TryParse(_envPort, out var _ev)) _parsedPort = _ev;
}

if (_parsedPort.HasValue)
{
    var _urls = $"http://0.0.0.0:{_parsedPort.Value}";
    Environment.SetEnvironmentVariable("ASPNETCORE_URLS", _urls);
    Console.WriteLine($"[bootstrap] ASPNETCORE_URLS set to {_urls} (from port parameter)");
}

var builder = WebApplication.CreateBuilder(args);

// Reduce verbose EF Core SQL logs: show only warnings or above for EF Core categories
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", Microsoft.Extensions.Logging.LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", Microsoft.Extensions.Logging.LogLevel.Warning);

// Configure Serilog to write logs to a file with timestamps
var logsPath = Path.Combine(AppContext.BaseDirectory, "logs");
Directory.CreateDirectory(logsPath);

// If Serilog is available, configure it; otherwise fall back to default logging
try
{
    builder.Host.UseSerilog((ctx, lc) =>
    {
        lc
            .MinimumLevel.Information()
            // Avoid logging EF Core SQL text (Database.Command) at Information level
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", Serilog.Events.LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.File(
                Path.Combine(logsPath, "backend-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 10,
                shared: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}")
            .WriteTo.Console(outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}");
    });
}
catch { /* Ignore if Serilog not available at design-time */ }

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddControllers().AddJsonOptions(opts =>
{
    opts.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});

// Windows Authentication (Negotiate)
// Allow tests to disable Negotiate by setting TEST_DISABLE_NEGOTIATE=1 in the process environment.
var disableNegotiateEnv = Environment.GetEnvironmentVariable("TEST_DISABLE_NEGOTIATE");
var disableNegotiate = string.Equals(disableNegotiateEnv, "1", StringComparison.OrdinalIgnoreCase)
    || string.Equals(disableNegotiateEnv, "true", StringComparison.OrdinalIgnoreCase);

if (!disableNegotiate)
{
    builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.Negotiate.NegotiateDefaults.AuthenticationScheme)
        .AddNegotiate();
}
else
{
    // In test mode, the test host will register its own Authentication scheme (e.g. 'Test').
    builder.Services.AddAuthentication();
}

builder.Services.AddAuthorization();

// Add CORS policy for frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy => policy
            // allow the local dev origins that clients will use (localhost, 127.0.0.1 and the server hostname)
            .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173", "http://shai571a:5173", "http://localhost:5080", "http://shai571a:5080")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

// Register WorkSessionManager singleton so it can manage in-memory sessions across the app
builder.Services.AddSingleton<backend.Services.WorkSessionManager>();

builder.Services.AddHostedService<backend.Services.WorkSessionCleanupService>();

// Configure EF Core DbContext via DI (pooled). Reads DbPath/DbProvider from appsettings.json.
try
{
    var dbProvider = (builder.Configuration["DbProvider"] ?? "sqlite").ToLowerInvariant();
    var dbPathConfig = builder.Configuration["DbPath"];
    string connString;
    if (!string.IsNullOrWhiteSpace(dbPathConfig))
    {
        var dbPath = Path.IsPathRooted(dbPathConfig)
            ? dbPathConfig
            : Path.Combine(AppContext.BaseDirectory, dbPathConfig);
        connString = $"Data Source={dbPath}";
    }
    else
    {
        connString = AppDbContext.ConnectionString;
    }

    if (dbProvider == "sqlserver")
    {
        builder.Services.AddDbContextPool<AppDbContext>(options => options.UseSqlServer(connString), poolSize: 64);
    }
    else
    {
        builder.Services.AddDbContextPool<AppDbContext>(options => options.UseSqlite(connString), poolSize: 64);
    }
}
catch { /* fallback to AppDbContext.OnConfiguring if DI setup fails */ }

// DbContext registered above via AddDbContextPool with options; no additional registration needed here.

var app = builder.Build();

// Load roles config
var rolesConfigPath = Path.Combine(AppContext.BaseDirectory, "RolesConfig.json");
RoleConfig rolesConfig = new RoleConfig();
if (System.IO.File.Exists(rolesConfigPath))
{
    try
    {
        var json = System.IO.File.ReadAllText(rolesConfigPath);
        rolesConfig = System.Text.Json.JsonSerializer.Deserialize<RoleConfig>(json) ?? rolesConfig;
    }
    catch { }
}

// Use CORS in development (also applied globally below)
if (app.Environment.IsDevelopment())
{
    app.UseCors("AllowFrontend");
    app.MapOpenApi();
}

// Ensure CORS runs for all environments before auth
app.UseCors("AllowFrontend");

// Short-circuit OPTIONS preflight to avoid Negotiate handshake issues
// app.Use(async (context, next) =>
// {
//     if (string.Equals(context.Request.Method, "OPTIONS", StringComparison.OrdinalIgnoreCase))
//     {
//         context.Response.StatusCode = 200;
//         return;
//     }
//     await next();
// });

app.UseAuthentication();
app.UseAuthorization();

// SPA fallback: for any non-API request without a file extension, serve index.html
// Run the fallback before static files so deep routes (e.g. /planning) return the SPA
app.Use(async (context, next) =>
{
    // If the request is not for /api and does not contain a file extension, rewrite to /index.html
    var path = context.Request.Path.Value ?? string.Empty;
    if (!path.StartsWith("/api", StringComparison.OrdinalIgnoreCase) && !System.IO.Path.HasExtension(path))
    {
        context.Request.Path = "/index.html";
    }
    await next();
});

// Ensure static files (wwwroot) are served so the built SPA can be placed in the backend publish folder
app.UseDefaultFiles(); // enables default file mapping (index.html)
app.UseStaticFiles();  // serve files from wwwroot

// Helper that builds the response object for current user
static (string user, string displayName, List<string> roles) BuildUserResponse(string? effectiveUser, string? displayName, RoleConfig rolesConfig)
{
    effectiveUser ??= "Guest";
    displayName ??= effectiveUser;

    static string Norm(string s) => (s ?? string.Empty).Trim().ToLowerInvariant();
    var candidates = new[]
    {
        displayName,
        effectiveUser,
        ExtractSamAccountName(effectiveUser)
    }
    .Where(s => !string.IsNullOrWhiteSpace(s))
    .Select(Norm)
    .ToHashSet();

    bool inAdmins = rolesConfig.Administrators.Any(r => candidates.Contains(Norm(r)));
    bool inPMs    = rolesConfig.ProductionManagers.Any(r => candidates.Contains(Norm(r)));
    bool inPEs    = rolesConfig.ProcessEngineers.Any(r => candidates.Contains(Norm(r)));
    bool inWorkers= rolesConfig.Workers.Any(r => candidates.Contains(Norm(r)));

    var roles = new List<string>();
    if (inAdmins) roles.Add("Administrator");
    if (inPMs) roles.Add("ProductionManager");
    if (inPEs) roles.Add("ProcessEngineer");
    if (inWorkers) roles.Add("Worker");

    return (effectiveUser, displayName, roles);
}

// Dev-only or environment-sensitive mapping for current-user
if (app.Environment.IsDevelopment())
{
    app.MapGet("/api/auth/current-user", async (HttpContext http, ILogger<Program> logger, IHostEnvironment env) =>
    {
        var req = http.Request;
        var identity = http.User?.Identity;

        string? devHeader = req.Headers["X-Dev-User"].FirstOrDefault();
        string? devQuery = req.Query["devUser"].FirstOrDefault();

        var origin = req.Headers["Origin"].FirstOrDefault();
        bool isRemoteDevOrigin = string.Equals(origin, "http://shai571a:5173", StringComparison.OrdinalIgnoreCase);

        // If request comes from the remote dev origin and there is no dev override,
        // force an authentication challenge so the browser will perform Negotiate.
        if (isRemoteDevOrigin && string.IsNullOrWhiteSpace(devHeader) && string.IsNullOrWhiteSpace(devQuery) && !(identity?.IsAuthenticated ?? false))
        {
            await http.ChallengeAsync();
            return Results.StatusCode(401);
        }

        bool hasAuthHeader = req.Headers.ContainsKey("Authorization");
        int cookieCount = req.Cookies?.Count ?? 0;

        logger.LogInformation(
            "GET /api/auth/current-user: RemoteIP={RemoteIP}, IsAuthenticated={IsAuth}, IdentityName={Name}, AuthType={AuthType}, HasAuthHeader={HasAuthHeader}, CookieCount={CookieCount}, Origin={Origin}, Referer={Referer}, XDevUserHeader={XDevUser}, DevUserQuery={DevUserQuery}, HeaderKeys={HeaderKeys}",
            http.Connection.RemoteIpAddress?.ToString() ?? "(null)",
            identity?.IsAuthenticated ?? false,
            identity?.Name ?? "(null)",
            identity?.AuthenticationType ?? "(null)",
            hasAuthHeader,
            cookieCount,
            origin ?? "(null)",
            req.Headers["Referer"].FirstOrDefault() ?? "(null)",
            devHeader ?? "(null)",
            devQuery ?? "(null)",
            string.Join(",", req.Headers.Select(h => h.Key))
        );

        string? effectiveUser = null;
        string? displayName = null;

        if (!string.IsNullOrWhiteSpace(devHeader) || !string.IsNullOrWhiteSpace(devQuery))
        {
            effectiveUser = !string.IsNullOrWhiteSpace(devHeader) ? devHeader : devQuery;
            displayName = effectiveUser;
        }

        if (effectiveUser is null && identity is { IsAuthenticated: true, Name: not null })
        {
            effectiveUser = identity.Name; // DOMAIN\\sam or UPN
            try
            {
#if WINDOWS
                var sam = ExtractSamAccountName(effectiveUser);
                logger.LogInformation("Extracted SAM account name: {SamAccountName}", sam);
                using var ctx = new System.DirectoryServices.AccountManagement.PrincipalContext(System.DirectoryServices.AccountManagement.ContextType.Domain);
                using var user = System.DirectoryServices.AccountManagement.UserPrincipal.FindByIdentity(ctx, System.DirectoryServices.AccountManagement.IdentityType.SamAccountName, sam)
                                ?? System.DirectoryServices.AccountManagement.UserPrincipal.FindByIdentity(ctx, effectiveUser);
                logger.LogInformation("Resolved user display name: {DisplayName}", user?.DisplayName ?? "(null)");
                displayName = user?.DisplayName ?? effectiveUser;
#else
                displayName = effectiveUser;
#endif
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to resolve display name for {User}", effectiveUser);
                displayName = effectiveUser;
            }
        }

        var (user, disp, roles) = BuildUserResponse(effectiveUser, displayName, rolesConfig);
        return Results.Ok(new { user, displayName = disp, roles });

    }).AllowAnonymous();
}
else
{
    app.MapGet("/api/auth/current-user", (HttpContext http, ILogger<Program> logger, IHostEnvironment env) =>
    {
        var req = http.Request;
        var identity = http.User?.Identity;

        string? devHeader = req.Headers["X-Dev-User"].FirstOrDefault();
        string? devQuery = req.Query["devUser"].FirstOrDefault();

        bool hasAuthHeader = req.Headers.ContainsKey("Authorization");
        int cookieCount = req.Cookies?.Count ?? 0;

        logger.LogInformation(
            "GET /api/auth/current-user: RemoteIP={RemoteIP}, IsAuthenticated={IsAuth}, IdentityName={Name}, AuthType={AuthType}, HasAuthHeader={HasAuthHeader}, CookieCount={CookieCount}, Origin={Origin}, Referer={Referer}, XDevUserHeader={XDevUser}, DevUserQuery={DevUserQuery}, HeaderKeys={HeaderKeys}",
            http.Connection.RemoteIpAddress?.ToString() ?? "(null)",
            identity?.IsAuthenticated ?? false,
            identity?.Name ?? "(null)",
            identity?.AuthenticationType ?? "(null)",
            hasAuthHeader,
            cookieCount,
            req.Headers["Origin"].FirstOrDefault() ?? "(null)",
            req.Headers["Referer"].FirstOrDefault() ?? "(null)",
            devHeader ?? "(null)",
            devQuery ?? "(null)",
            string.Join(",", req.Headers.Select(h => h.Key))
        );

        string? effectiveUser = null;
        string? displayName = null;

        if (!string.IsNullOrWhiteSpace(devHeader) || !string.IsNullOrWhiteSpace(devQuery))
        {
            if (env.IsDevelopment())
            {
                effectiveUser = !string.IsNullOrWhiteSpace(devHeader) ? devHeader : devQuery;
                displayName = effectiveUser;
            }
            else
            {
                logger.LogWarning("Dev override attempted outside Development. Ignored.");
            }
        }

        if (effectiveUser is null && identity is { IsAuthenticated: true, Name: not null })
        {
            effectiveUser = identity.Name; // DOMAIN\\sam or UPN
            try
            {
#if WINDOWS
                var sam = ExtractSamAccountName(effectiveUser);
                logger.LogInformation("Extracted SAM account name: {SamAccountName}", sam);
                using var ctx = new System.DirectoryServices.AccountManagement.PrincipalContext(System.DirectoryServices.AccountManagement.ContextType.Domain);
                using var user = System.DirectoryServices.AccountManagement.UserPrincipal.FindByIdentity(ctx, System.DirectoryServices.AccountManagement.IdentityType.SamAccountName, sam)
                                ?? System.DirectoryServices.AccountManagement.UserPrincipal.FindByIdentity(ctx, effectiveUser);
                logger.LogInformation("Resolved user display name: {DisplayName}", user?.DisplayName ?? "(null)");
                displayName = user?.DisplayName ?? effectiveUser;
#else
                displayName = effectiveUser;
#endif
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to resolve display name for {User}", effectiveUser);
                displayName = effectiveUser;
            }
        }

        var (user, disp, roles) = BuildUserResponse(effectiveUser, displayName, rolesConfig);
        return Results.Ok(new { user, displayName = disp, roles });

    }).RequireAuthorization();
}

// Dedicated endpoint that triggers a Negotiate/Windows authentication challenge.
// Clients (especially remote dev browsers) can call this endpoint first to receive
// a 401 + WWW-Authenticate response (with CORS headers applied) so the browser
// will perform the Negotiate handshake and send credentials on the follow-up request.
app.MapGet("/api/auth/challenge", async (HttpContext http, ILogger<Program> logger) =>
{
    logger.LogInformation("GET /api/auth/challenge: RemoteIP={RemoteIP}, Origin={Origin}, HeaderKeys={HeaderKeys}",
        http.Connection.RemoteIpAddress?.ToString() ?? "(null)",
        http.Request.Headers["Origin"].FirstOrDefault() ?? "(null)",
        string.Join(',', http.Request.Headers.Select(h => h.Key)));

    // Issue an authentication challenge (Negotiate). The authentication middleware
    // will attach the appropriate WWW-Authenticate header(s). Return 401 so the
    // browser knows to retry with credentials.
    await http.ChallengeAsync();
    return Results.StatusCode(401);
});

// Local helpers
static string ExtractSamAccountName(string domainQualified)
{
    if (string.IsNullOrWhiteSpace(domainQualified)) return domainQualified;
    var idx = domainQualified.IndexOf('\\');
    return idx >= 0 && idx + 1 < domainQualified.Length
        ? domainQualified[(idx + 1)..]
        : domainQualified;
}

// Endpoint to get roles configuration
app.MapGet("/api/auth/roles", () => Results.Ok(rolesConfig)).AllowAnonymous();

// Endpoint to find a user by Gid, FullName or Mail (case-insensitive)
app.MapGet("/api/auth/find-user", (HttpRequest http, ILogger<Program> logger, IServiceScopeFactory scopeFactory) =>
{
    var q = http.Query["q"].FirstOrDefault()?.Trim();
    if (string.IsNullOrWhiteSpace(q)) return Results.BadRequest(new { message = "q query parameter is required" });
    var qn = q.ToLowerInvariant();
    try
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<backend.Data.AppDbContext>();
        var user = db.Users.AsNoTracking()
            .FirstOrDefault(u =>
                (!string.IsNullOrWhiteSpace(u.Gid) && u.Gid.ToLower().Contains(qn)) ||
                (!string.IsNullOrWhiteSpace(u.FullName) && u.FullName.ToLower().Contains(qn)) ||
                (!string.IsNullOrWhiteSpace(u.Mail) && u.Mail.ToLower().Contains(qn))
            );
        if (user == null) return Results.NotFound();
        return Results.Ok(new { gid = user.Gid, fullName = user.FullName, mail = user.Mail, role = user.Role });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error while searching for user. Query={Query}", q);
        return Results.Problem(ex.Message);
    }
}).AllowAnonymous();

app.MapControllers();

app.Run();

// Role config POCO to avoid dynamic dispatch
public class RoleConfig
{
    public List<string> Administrators { get; set; } = new List<string>();
    public List<string> ProductionManagers { get; set; } = new List<string>();
    public List<string> ProcessEngineers { get; set; } = new List<string>();
    public List<string> Workers { get; set; } = new List<string>();
}

// Expose Program type for WebApplicationFactory in tests
public partial class Program { }
