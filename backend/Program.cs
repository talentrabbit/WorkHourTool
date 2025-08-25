using Serilog;
// using System.DirectoryServices.AccountManagement; // Optional: requires Windows; guarded usage below

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog to write logs to a file with timestamps
var logsPath = Path.Combine(AppContext.BaseDirectory, "Logs");
Directory.CreateDirectory(logsPath);

// If Serilog is available, configure it; otherwise fall back to default logging
try
{
    builder.Host.UseSerilog((ctx, lc) =>
    {
        lc
            .MinimumLevel.Information()
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
builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.Negotiate.NegotiateDefaults.AuthenticationScheme)
    .AddNegotiate();

builder.Services.AddAuthorization();

// Add CORS policy for frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy => policy
            .WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

var app = builder.Build();

// Load roles config
var rolesConfigPath = Path.Combine(AppContext.BaseDirectory, "RolesConfig.json");
var rolesConfig = new { Administrators = new List<string>(), ProductionManagers = new List<string>(), ProcessEngineers = new List<string>(), Workers = new List<string>() };
if (System.IO.File.Exists(rolesConfigPath))
{
    try
    {
        var json = System.IO.File.ReadAllText(rolesConfigPath);
        rolesConfig = System.Text.Json.JsonSerializer.Deserialize(json, rolesConfig.GetType()) as dynamic ?? rolesConfig;
    }
    catch { }
}

// Use CORS in development
if (app.Environment.IsDevelopment())
{
    app.UseCors("AllowFrontend");
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

// Dev-only override current user via query/header for testing
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

logger.LogInformation("Effective user: {EffectiveUser}, Display name: {DisplayName}", effectiveUser, displayName);
    if (effectiveUser is null && identity is { IsAuthenticated: true, Name: not null })
    {
        effectiveUser = identity.Name; // DOMAIN\\sam or UPN
        try
        {
#if WINDOWS
            // Resolve display name from AD if available
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

    effectiveUser ??= "Guest";
    displayName ??= effectiveUser;

    // Role resolution: match against multiple identifiers
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

    return Results.Ok(new { user = effectiveUser, displayName, roles });
}).RequireAuthorization();

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

app.MapControllers();
app.Run();
