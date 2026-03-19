using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using VpnProductPlatform.Application.Abstractions;

namespace VpnProductPlatform.Infrastructure.Security;

internal sealed class RefreshTokenService(
    IOptions<JwtOptions> options,
    IClock clock) : IRefreshTokenService
{
    private readonly JwtOptions _options = options.Value;

    public RefreshTokenEnvelope Issue(Guid sessionId)
    {
        Span<byte> buffer = stackalloc byte[32];
        RandomNumberGenerator.Fill(buffer);

        var secret = ToBase64Url(buffer);
        var token = $"{sessionId:N}.{secret}";
        var tokenHash = HashSecret(secret);
        var expiresAtUtc = clock.UtcNow.AddDays(_options.RefreshLifetimeDays);

        return new RefreshTokenEnvelope(token, tokenHash, expiresAtUtc);
    }

    public bool TryGetSessionId(string refreshToken, out Guid sessionId)
    {
        sessionId = Guid.Empty;
        if (!TrySplit(refreshToken, out var sessionTokenId, out _))
        {
            return false;
        }

        return Guid.TryParseExact(sessionTokenId, "N", out sessionId)
            || Guid.TryParse(sessionTokenId, out sessionId);
    }

    public bool Verify(string refreshToken, string expectedTokenHash)
    {
        if (!TrySplit(refreshToken, out _, out var secret))
        {
            return false;
        }

        var actualHash = HashSecret(secret);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(actualHash),
            Encoding.UTF8.GetBytes(expectedTokenHash));
    }

    private static bool TrySplit(string refreshToken, out string sessionId, out string secret)
    {
        sessionId = string.Empty;
        secret = string.Empty;

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return false;
        }

        var parts = refreshToken.Trim().Split('.', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        sessionId = parts[0];
        secret = parts[1];
        return !string.IsNullOrWhiteSpace(sessionId) && !string.IsNullOrWhiteSpace(secret);
    }

    private static string HashSecret(string secret)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(bytes);
    }

    private static string ToBase64Url(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
