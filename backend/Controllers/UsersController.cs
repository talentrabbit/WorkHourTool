using backend.Contracts;
using backend.Security;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Policy = DatabaseAdministratorRequirement.PolicyName)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class UsersController(IUserAdministrationService users) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("List users")]
    [EndpointDescription("Requires an authenticated account with the Administrator role in the Users table.")]
    [ProducesResponseType<IReadOnlyList<UserResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> List(CancellationToken cancellationToken) =>
        Ok(await users.ListAsync(cancellationToken));

    [HttpGet("{id:int}")]
    [EndpointSummary("Get a user by ID")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var user = await users.GetAsync(id, cancellationToken);
        return user is null ? Problem(statusCode: 404, detail: "User not found.") : Ok(user);
    }

    [HttpPost]
    [EndpointSummary("Create a user")]
    [EndpointDescription("Trims input, validates canonical roles and rejects case-insensitive duplicate Gid or nonempty mail.")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserResponse>> Create(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var result = await users.CreateAsync(request, cancellationToken);
        return result.Status == UserMutationStatus.Conflict
            ? Problem(statusCode: 409, detail: result.Detail)
            : CreatedAtAction(nameof(GetById), new { id = result.User!.Id }, result.User);
    }

    [HttpPut("{id:int}")]
    [EndpointSummary("Update a user's email and roles")]
    [EndpointDescription("Updates only mail and canonical roles; rejects duplicate nonempty mail and removal of Administrator from the current account or last administrator.")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserResponse>> Update(int id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var result = await users.UpdateAsync(id, request, User, cancellationToken);
        return result.Status switch
        {
            UserMutationStatus.NotFound => Problem(statusCode: 404, detail: "User not found."),
            UserMutationStatus.Conflict => Problem(statusCode: 409, detail: result.Detail),
            _ => Ok(result.User)
        };
    }

    [HttpDelete("{id:int}")]
    [EndpointSummary("Delete a user")]
    [EndpointDescription("Cannot delete the authenticated account or the last database administrator.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await users.DeleteAsync(id, User, cancellationToken);
        return result.Status switch
        {
            UserMutationStatus.NotFound => Problem(statusCode: 404, detail: "User not found."),
            UserMutationStatus.Conflict => Problem(statusCode: 409, detail: result.Detail),
            _ => NoContent()
        };
    }
}