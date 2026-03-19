using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using VpnProductPlatform.Application.Abstractions;

namespace VpnProductPlatform.Infrastructure.Security;

internal sealed class JwtTokenIssuer(IOptions<JwtOptions> options) : ITokenIssuer
{
    private readonly JwtOptions _options = options.Value;

    public (string Token, DateTimeOffset ExpiresAtUtc) Issue(Guid accountId, string email)
    {
        var expiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(_options.LifetimeMinutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, accountId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(ClaimTypes.NameIdentifier, accountId.ToString()),
            new Claim(ClaimTypes.Email, email)
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expiresAtUtc.UtcDateTime,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
    }
}
