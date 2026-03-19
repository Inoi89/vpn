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

    [HttpGet("nodes")]
    public async Task<ActionResult<IReadOnlyList<IssuableNodeResponse>>> GetIssuableNodes(CancellationToken cancellationToken)
    {
        try
        {
            var nodes = await accessGrantApplicationService.ListIssuableNodesAsync(cancellationToken);
            return Ok(nodes);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<IssuedAccessGrantResponse>> Issue(
        [FromBody] IssueAccessGrantRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await accessGrantApplicationService.IssueAsync(User.GetRequiredAccountId(), request, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }
}
