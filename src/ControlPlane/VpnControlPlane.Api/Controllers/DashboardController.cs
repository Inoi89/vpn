using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VpnControlPlane.Application.Abstractions;
using VpnControlPlane.Application.Dashboard.Queries;

namespace VpnControlPlane.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/dashboard")]
public sealed class DashboardController(IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<VpnControlPlane.Application.DashboardSnapshotDto>> GetDashboard(
        [FromQuery] int trafficPoints = 100,
        CancellationToken cancellationToken = default)
    {
        var dashboard = await queryDispatcher.Query(new GetDashboardQuery(trafficPoints), cancellationToken);
        return Ok(dashboard);
    }
}
