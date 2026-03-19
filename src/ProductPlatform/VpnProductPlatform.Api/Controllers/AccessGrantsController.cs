using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VpnProductPlatform.Api.Infrastructure;
using VpnProductPlatform.Application.AccessGrants;
using VpnProductPlatform.Contracts;

namespace VpnProductPlatform.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/access-grants")]
public sealed class AccessGrantsController(AccessGrantApplicationService accessGrantApplicationService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AccessGrantResponse>>> Get(CancellationToken cancellationToken)
    {
        var grants = await accessGrantApplicationService.ListAsync(User.GetRequiredAccountId(), cancellationToken);
        return Ok(grants);
    }
}
