namespace VpnProductPlatform.Application.Abstractions;

public sealed record AuthSessionContext(
    string? IpAddress,
    string? UserAgent);

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public interface IPasswordHashService
{
    string HashPassword(string subject, string password);
    bool VerifyHashedPassword(string subject, string passwordHash, string password);
}

public interface ITokenIssuer
{
    (string Token, DateTimeOffset ExpiresAtUtc) Issue(Guid accountId, string email, Guid sessionId);
}

public sealed record RefreshTokenEnvelope(
    string Token,
    string TokenHash,
    DateTimeOffset ExpiresAtUtc);

public interface IRefreshTokenService
{
    RefreshTokenEnvelope Issue(Guid sessionId);
    bool TryGetSessionId(string refreshToken, out Guid sessionId);
    bool Verify(string refreshToken, string expectedTokenHash);
}

public interface IAccountEmailService
{
    Task SendVerificationAsync(
        string email,
        string displayName,
        string verificationToken,
        CancellationToken cancellationToken);
}

public sealed record EmailVerificationTokenEnvelope(
    string Token,
    DateTimeOffset ExpiresAtUtc);

public sealed record EmailVerificationPayload(
    Guid AccountId,
    string Email,
    DateTimeOffset ExpiresAtUtc);

public interface IEmailVerificationTokenService
{
    EmailVerificationTokenEnvelope Issue(Guid accountId, string email);
    bool TryValidate(string token, out EmailVerificationPayload? payload);
}
