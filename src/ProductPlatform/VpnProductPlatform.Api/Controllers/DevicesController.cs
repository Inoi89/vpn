using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VpnProductPlatform.Api.Infrastructure;
using VpnProductPlatform.Application.Devices;
using VpnProductPlatform.Contracts;

namespace VpnProductPlatform.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/devices")]
public sealed class DevicesController(DeviceApplicationService deviceApplicationService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DeviceResponse>>> Get(CancellationToken cancellationToken)
    {
        var devices = await deviceApplicationService.ListAsync(User.GetRequiredAccountId(), cancellationToken);
        return Ok(devices);
    }

    [HttpPost]
    public async Task<ActionResult<DeviceResponse>> Register(
        [FromBody] RegisterDeviceRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var device = await deviceApplicationService.RegisterAsync(User.GetRequiredAccountId(), request, cancellationToken);
            return Ok(device);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpDelete("{deviceId:guid}")]
    public async Task<IActionResult> Revoke(Guid deviceId, CancellationToken cancellationToken)
    {
        try
        {
            await deviceApplicationService.RevokeAsync(User.GetRequiredAccountId(), deviceId, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }
}
