using System.Security.Claims;
using backend.Data;
using backend.DbModel;
using backend.Security;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

/// <summary>Resolves server-authenticated accounts only; never reads request headers or role claims.</summary>
public sealed class CurrentUserResolver(AppDbContext db, ILogger<CurrentUserResolver> logger) : ICurrentUserResolver
{
    public async Task<IReadOnlyList<User>> ResolveAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var name = principal.Identity is { IsAuthenticated: true, Name: not null } identity
            ? identity.Name.Trim() : null;
        if (string.IsNullOrWhiteSpace(name)) return Array.Empty<User>();

        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            name, PrincipalAccount.ExtractSamAccountName(name)
        };

        // Same optional Windows directory lookup as current-user; never accept a client display name.
    if (OperatingSystem.IsWindows() && principal.Identity is System.Security.Principal.WindowsIdentity)
    {
        try
        {
            using var context = new System.DirectoryServices.AccountManagement.PrincipalContext(
                System.DirectoryServices.AccountManagement.ContextType.Domain);
            using var user = System.DirectoryServices.AccountManagement.UserPrincipal.FindByIdentity(context,
                System.DirectoryServices.AccountManagement.IdentityType.SamAccountName,
                PrincipalAccount.ExtractSamAccountName(name))
                ?? System.DirectoryServices.AccountManagement.UserPrincipal.FindByIdentity(context, name);
            if (!string.IsNullOrWhiteSpace(user?.DisplayName)) candidates.Add(user.DisplayName.Trim());
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to resolve directory display name for {User}", name);
        }
    }
        // Compare in .NET for identical, exact case-insensitive semantics on SQLite and SQL Server.
        // This also preserves legacy rows whose fields contain surrounding whitespace.
        var users = await db.Users.AsNoTracking().ToListAsync(cancellationToken);
        var matches = users.Where(u => candidates.Contains(u.Gid?.Trim() ?? string.Empty)
            || candidates.Contains(u.FullName?.Trim() ?? string.Empty)
            || candidates.Contains(u.Mail?.Trim() ?? string.Empty)).ToArray();
        logger.LogDebug("Resolved {Count} database account matches for authenticated principal {User}", matches.Length, name);
        return matches;
    }
}