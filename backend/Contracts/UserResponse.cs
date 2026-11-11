namespace backend.Contracts;

/// <summary>A database user with distinct canonical roles instead of the legacy comma-separated role field.</summary>
public sealed record UserResponse(int Id, string? Gid, string? FullName, string? Mail, string[] Roles)
{
	/// <summary>Original stored roles for directory display, including legacy values. Not used to authorize requests.</summary>
	public string? Role { get; init; }
}