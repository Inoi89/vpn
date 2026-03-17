using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VpnControlPlane.Application.Abstractions;
using VpnControlPlane.Application.Users.Commands;
using VpnControlPlane.Application.Users.Queries;

namespace VpnControlPlane.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/users")]
public sealed class UsersController(ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<VpnControlPlane.Application.UserSummaryDto>>> GetUsers(CancellationToken cancellationToken)
    {
        var users = await queryDispatcher.Query(new GetUsersQuery(), cancellationToken);
        return Ok(users);
    }

    [HttpPost]
    public async Task<ActionResult<VpnControlPlane.Application.UserSummaryDto>> UpsertUser(
        [FromBody] UpsertUserRequest request,
        CancellationToken cancellationToken)
    {
        var user = await commandDispatcher.Send(
            new CreateUserCommand(request.ExternalId, request.DisplayName, request.Email, request.IsEnabled),
            cancellationToken);

        return Ok(user);
    }
}

public sealed record UpsertUserRequest(
    string ExternalId,
    string DisplayName,
    string? Email,
    bool IsEnabled = true);
