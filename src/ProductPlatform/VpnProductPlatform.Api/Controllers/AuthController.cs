using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VpnProductPlatform.Api.Infrastructure;
using VpnProductPlatform.Application.Accounts;
using VpnProductPlatform.Application.Abstractions;
using VpnProductPlatform.Contracts;

namespace VpnProductPlatform.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    AccountApplicationService accountApplicationService,
    SessionApplicationService sessionApplicationService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<AuthTokenResponse>> Register(
        [FromBody] RegisterAccountRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await accountApplicationService.RegisterAsync(request, BuildSessionContext(), cancellationToken);
            return Ok(response);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthTokenResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await accountApplicationService.LoginAsync(request, BuildSessionContext(), cancellationToken);
            return Ok(response);
        }
        catch (InvalidOperationException exception)
        {
            return Unauthorized(new { error = exception.Message });
        }
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthTokenResponse>> Refresh(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await sessionApplicationService.RefreshAsync(request, BuildSessionContext(), cancellationToken);
            return Ok(response);
        }
        catch (InvalidOperationException exception)
        {
            return Unauthorized(new { error = exception.Message });
        }
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        try
        {
            await sessionApplicationService.RevokeAsync(
                User.GetRequiredAccountId(),
                User.GetRequiredSessionId(),
                "User logout",
                cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    private AuthSessionContext BuildSessionContext()
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();
        return new AuthSessionContext(ipAddress, userAgent);
    }
}
