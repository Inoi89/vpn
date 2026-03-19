using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using VpnProductPlatform.Application.Abstractions;

namespace VpnProductPlatform.Infrastructure.Security;

public sealed class EmailVerificationTokenService(IOptions<EmailVerificationOptions> options) : IEmailVerificationTokenService
{
    private readonly EmailVerificationOptions _options = options.Value;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();

    public EmailVerificationTokenEnvelope Issue(Guid accountId, string email)
    {
        var expiresAtUtc = DateTimeOffset.UtcNow.AddHours(_options.LifetimeHours);
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([
                new Claim(JwtRegisteredClaimNames.Sub, accountId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, email),
            ]),
            Expires = expiresAtUtc.UtcDateTime,
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
                SecurityAlgorithms.HmacSha256)
        };

        var token = _tokenHandler.CreateToken(descriptor);
        return new EmailVerificationTokenEnvelope(_tokenHandler.WriteToken(token), expiresAtUtc);
    }

    public bool TryValidate(string token, out EmailVerificationPayload? payload)
    {
        payload = null;

        try
        {
            var principal = _tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                ValidIssuer = _options.Issuer,
                ValidAudience = _options.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
                ClockSkew = TimeSpan.FromMinutes(1)
            }, out var validatedToken);

            var accountIdValue = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var email = principal.FindFirstValue(JwtRegisteredClaimNames.Email)
                ?? principal.FindFirstValue(ClaimTypes.Email);

            if (!Guid.TryParse(accountIdValue, out var accountId) || string.IsNullOrWhiteSpace(email))
            {
                return false;
            }

            var jwtToken = (JwtSecurityToken)validatedToken;
            var expiresAt = jwtToken.ValidTo == DateTime.MinValue
                ? DateTimeOffset.UtcNow
                : new DateTimeOffset(jwtToken.ValidTo, TimeSpan.Zero);

            payload = new EmailVerificationPayload(accountId, email, expiresAt);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
