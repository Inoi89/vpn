using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VpnProductPlatform.Api.Infrastructure;
using VpnProductPlatform.Application.Accounts;
using VpnProductPlatform.Contracts;

namespace VpnProductPlatform.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/me")]
public sealed class MeController(AccountApplicationService accountApplicationService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<MeResponse>> Get(CancellationToken cancellationToken)
    {
        try
        {
            var response = await accountApplicationService.GetCurrentAsync(User.GetRequiredAccountId(), cancellationToken);
            return Ok(response);
        }
        catch (InvalidOperationException exception)
        {
            return NotFound(new { error = exception.Message });
        }
    }
}
