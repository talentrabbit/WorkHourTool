using System.ComponentModel.DataAnnotations;
using backend.Security;

namespace backend.Contracts;

/// <summary>Creates a database user with one or more recognized roles; text fields are trimmed before validation.</summary>
public sealed record CreateUserRequest : IValidatableObject
{
    private string? _gid;
    private string _fullName = string.Empty;
    private string? _mail;
    private string[] _roles = Array.Empty<string>();

    public string? Gid { get => _gid; init => _gid = string.IsNullOrWhiteSpace(value) ? null : value.Trim(); }

    [Required, MaxLength(200)]
    public string FullName { get => _fullName; init => _fullName = value?.Trim() ?? string.Empty; }

    // Validate optional nullable mail below: .NET 9 OpenAPI cannot apply length attributes
    // to the ["string", "null"] schema emitted for nullable properties.
    public string? Mail { get => _mail; init => _mail = string.IsNullOrWhiteSpace(value) ? null : value.Trim(); }

    [Required, MinLength(1)]
    public string[] Roles { get => _roles; init => _roles = value ?? Array.Empty<string>(); }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Gid is not null && Gid.Length > 100)
            yield return new ValidationResult("Gid must be at most 100 characters.", new[] { nameof(Gid) });
        if (Mail is not null && (Mail.Length > 200 || !new EmailAddressAttribute().IsValid(Mail)))
            yield return new ValidationResult("Mail must be a valid email address of at most 200 characters.",
                new[] { nameof(Mail) });
        if (Roles is not null && Roles.Any(role => UserRoles.CanonicalRole(role) is null))
            yield return new ValidationResult(
                "Each role must be Administrator, ProductionManager, ProcessEngineer or Worker (legacy aliases accepted).",
                new[] { nameof(Roles) });
    }
}