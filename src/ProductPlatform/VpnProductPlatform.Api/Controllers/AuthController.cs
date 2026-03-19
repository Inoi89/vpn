using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VpnProductPlatform.Application.Accounts;
using VpnProductPlatform.Contracts;

namespace VpnProductPlatform.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(AccountApplicationService accountApplicationService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<AuthTokenResponse>> Register(
        [FromBody] RegisterAccountRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await accountApplicationService.RegisterAsync(request, cancellationToken);
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
            var response = await accountApplicationService.LoginAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (InvalidOperationException exception)
        {
            return Unauthorized(new { error = exception.Message });
        }
    }
}
