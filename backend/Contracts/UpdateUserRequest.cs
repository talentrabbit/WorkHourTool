using System.ComponentModel.DataAnnotations;
using backend.Security;

namespace backend.Contracts;

/// <summary>Updates only a user's optional email and required recognized roles; email is trimmed before validation.</summary>
public sealed record UpdateUserRequest : IValidatableObject
{
    private string? _mail;
    private string[] _roles = Array.Empty<string>();

    // Validate nullable mail below, matching CreateUserRequest's .NET 9 OpenAPI convention.
    public string? Mail { get => _mail; init => _mail = string.IsNullOrWhiteSpace(value) ? null : value.Trim(); }

    [Required, MinLength(1)]
    public string[] Roles { get => _roles; init => _roles = value ?? Array.Empty<string>(); }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Mail is not null && (Mail.Length > 200 || !new EmailAddressAttribute().IsValid(Mail)))
            yield return new ValidationResult("Mail must be a valid email address of at most 200 characters.",
                new[] { nameof(Mail) });
        if (Roles.Any(role => UserRoles.CanonicalRole(role) is null))
            yield return new ValidationResult(
                "Each role must be Administrator, ProductionManager, ProcessEngineer or Worker (legacy aliases accepted).",
                new[] { nameof(Roles) });
    }
}