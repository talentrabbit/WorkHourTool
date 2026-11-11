using System.Security.Claims;
using backend.Contracts;

namespace backend.Services;

public enum UserMutationStatus { Success, NotFound, Conflict }

public sealed record UserMutationResult(UserMutationStatus Status, UserResponse? User = null, string? Detail = null);

public interface IUserAdministrationService
{
    Task<IReadOnlyList<UserResponse>> ListAsync(CancellationToken cancellationToken);
    Task<UserResponse?> GetAsync(int id, CancellationToken cancellationToken);
    Task<UserMutationResult> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken);
    Task<UserMutationResult> UpdateAsync(int id, UpdateUserRequest request, ClaimsPrincipal principal, CancellationToken cancellationToken);
    Task<UserMutationResult> DeleteAsync(int id, ClaimsPrincipal principal, CancellationToken cancellationToken);
}