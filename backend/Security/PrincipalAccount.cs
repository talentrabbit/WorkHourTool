namespace backend.Security;

public static class PrincipalAccount
{
    public static string ExtractSamAccountName(string account)
    {
        var name = account.Trim();
        var slash = name.IndexOf('\\');
        if (slash >= 0 && slash + 1 < name.Length) name = name[(slash + 1)..];
        var at = name.IndexOf('@');
        return at > 0 ? name[..at] : name;
    }
}