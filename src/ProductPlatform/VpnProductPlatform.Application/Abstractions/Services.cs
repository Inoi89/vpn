namespace VpnProductPlatform.Application.Abstractions;

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
    (string Token, DateTimeOffset ExpiresAtUtc) Issue(Guid accountId, string email);
}
