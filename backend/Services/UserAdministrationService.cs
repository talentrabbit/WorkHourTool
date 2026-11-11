using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Claims;
using backend.Contracts;
using backend.Data;
using backend.DbModel;
using backend.Security;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public sealed class UserAdministrationService(AppDbContext db, ICurrentUserResolver currentUser)
    : IUserAdministrationService
{
    public async Task<IReadOnlyList<UserResponse>> ListAsync(CancellationToken cancellationToken) =>
        (await db.Users.AsNoTracking().OrderBy(u => u.Id).ToListAsync(cancellationToken)).Select(Map).ToArray();

    public async Task<UserResponse?> GetAsync(int id, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, cancellationToken);
        return user is null ? null : Map(user);
    }

    public async Task<UserMutationResult> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        Validator.ValidateObject(request, new ValidationContext(request), validateAllProperties: true);
        // SQLite starts a non-deferred transaction: concurrent writers cannot both pass the duplicate check.
        // Serializable also protects read/check/write invariants on the configured relational provider.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var users = await db.Users.AsNoTracking().ToListAsync(cancellationToken);
        if (request.Gid is not null && users.Any(u => Equal(u.Gid, request.Gid)))
            return new(UserMutationStatus.Conflict, Detail: "A user with this Gid already exists.");
        if (request.Mail is not null && users.Any(u => Equal(u.Mail, request.Mail)))
            return new(UserMutationStatus.Conflict, Detail: "A user with this mail address already exists.");

        var user = new User
        {
            // Older imported databases declare these optional columns NOT NULL.
            Gid = request.Gid ?? string.Empty,
            FullName = request.FullName,
            Mail = request.Mail ?? string.Empty,
            Role = string.Join(',', request.Roles!.Select(UserRoles.CanonicalRole).OfType<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase))
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(UserMutationStatus.Success, Map(user));
    }

    public async Task<UserMutationResult> UpdateAsync(int id, UpdateUserRequest request, ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        Validator.ValidateObject(request, new ValidationContext(request), validateAllProperties: true);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null) return new(UserMutationStatus.NotFound);

        var users = await db.Users.AsNoTracking().ToListAsync(cancellationToken);
        if (request.Mail is not null && users.Any(u => u.Id != id && Equal(u.Mail, request.Mail)))
            return new(UserMutationStatus.Conflict, Detail: "A user with this mail address already exists.");

        var roles = request.Roles.Select(UserRoles.CanonicalRole).OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (IsAdministrator(user) && !roles.Contains(UserRoles.Administrator))
        {
            // Resolve before changing mail, since mail can identify the authenticated account.
            var matchedUsers = await currentUser.ResolveAsync(principal, cancellationToken);
            if (matchedUsers.Any(u => u.Id == id))
                return new(UserMutationStatus.Conflict, Detail: "The current user cannot lose the Administrator role.");
            if (!users.Any(u => u.Id != id && IsAdministrator(u)))
                return new(UserMutationStatus.Conflict, Detail: "The last administrator cannot lose the Administrator role.");
        }

        // Imported databases can have a legacy NOT NULL mail column.
        user.Mail = request.Mail ?? string.Empty;
        user.Role = string.Join(',', roles);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(UserMutationStatus.Success, Map(user));
    }

    public async Task<UserMutationResult> DeleteAsync(int id, ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null) return new(UserMutationStatus.NotFound);

        var matchedUsers = await currentUser.ResolveAsync(principal, cancellationToken);
        if (matchedUsers.Any(u => u.Id == id))
            return new(UserMutationStatus.Conflict, Detail: "The current user cannot be deleted.");

        if (IsAdministrator(user))
        {
            var users = await db.Users.AsNoTracking().ToListAsync(cancellationToken);
            if (!users.Any(u => u.Id != id && IsAdministrator(u)))
                return new(UserMutationStatus.Conflict, Detail: "The last administrator cannot be deleted.");
        }

        db.Users.Remove(user);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(UserMutationStatus.Success);
    }

    private static bool Equal(string? left, string? right) =>
        string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool IsAdministrator(User user) =>
        UserRoles.ParseCanonicalRoles(user.Role).Contains(UserRoles.Administrator);

    private static UserResponse Map(User user) =>
        new(user.Id, OptionalText(user.Gid), user.FullName, OptionalText(user.Mail), UserRoles.ParseCanonicalRoles(user.Role).ToArray()) { Role = user.Role };

    private static string? OptionalText(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}