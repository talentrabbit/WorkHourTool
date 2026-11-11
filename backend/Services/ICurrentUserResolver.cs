using System.Security.Claims;
using backend.DbModel;

namespace backend.Services;

public interface ICurrentUserResolver
{
    Task<IReadOnlyList<User>> ResolveAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);
}