using backend.Services;
using Microsoft.AspNetCore.Authorization;

namespace backend.Security;

public sealed class DatabaseAdministratorRequirement : IAuthorizationRequirement
{
    public const string PolicyName = "DatabaseAdministrator";
}

public sealed class DatabaseAdministratorHandler(ICurrentUserResolver resolver)
    : AuthorizationHandler<DatabaseAdministratorRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        DatabaseAdministratorRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true) return;
        var cancellationToken = (context.Resource as HttpContext)?.RequestAborted ?? CancellationToken.None;
        var users = await resolver.ResolveAsync(context.User, cancellationToken);
        if (users.Any(u => UserRoles.ParseCanonicalRoles(u.Role).Contains(UserRoles.Administrator)))
            context.Succeed(requirement);
    }
}