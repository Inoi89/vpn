using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VpnProductPlatform.Api.Infrastructure;
using VpnProductPlatform.Application.Accounts;
using VpnProductPlatform.Contracts;

namespace VpnProductPlatform.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/sessions")]
public sealed class SessionsController(SessionApplicationService sessionApplicationService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SessionResponse>>> Get(CancellationToken cancellationToken)
    {
        var sessions = await sessionApplicationService.ListAsync(
            User.GetRequiredAccountId(),
            User.GetRequiredSessionId(),
            cancellationToken);
        return Ok(sessions);
    }

    [HttpDelete("{sessionId:guid}")]
    public async Task<IActionResult> Revoke(Guid sessionId, CancellationToken cancellationToken)
    {
        try
        {
            await sessionApplicationService.RevokeAsync(
                User.GetRequiredAccountId(),
                sessionId,
                "Revoked by user",
                cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }
}
