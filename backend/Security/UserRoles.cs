namespace backend.Security;

/// <summary>Canonical roles and legacy aliases used by authentication and user administration.</summary>
public static class UserRoles
{
    public const string Administrator = "Administrator";

    public static string? CanonicalRole(string? role)
    {
        var key = (role ?? string.Empty).Trim().ToLowerInvariant()
            .Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty);
        return key switch
        {
            "administrator" or "administrators" => Administrator,
            "productionmanager" or "productionmanagers" => "ProductionManager",
            "processengineer" or "processengineers" => "ProcessEngineer",
            "worker" or "workers" => "Worker",
            _ => null
        };
    }

    public static IEnumerable<string> ParseCanonicalRoles(string? roleText) =>
        (roleText ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(CanonicalRole).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase);
}