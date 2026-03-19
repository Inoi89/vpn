using VpnProductPlatform.Domain.Common;

namespace VpnProductPlatform.Domain.Entities;

public sealed class AccountSession : AuditableEntity
{
    private AccountSession()
    {
    }

    private AccountSession(
        Guid id,
        Guid accountId,
        string refreshTokenHash,
        DateTimeOffset expiresAtUtc,
        string? ipAddress,
        string? userAgent,
        DateTimeOffset now)
    {
        Id = id;
        AccountId = accountId;
        RefreshTokenHash = NormalizeRequired(refreshTokenHash, nameof(refreshTokenHash));
        ExpiresAtUtc = expiresAtUtc;
        IpAddress = NormalizeOptional(ipAddress, 64);
        UserAgent = NormalizeOptional(userAgent, 512);
        LastSeenAtUtc = now;
        MarkCreated(now);
    }

    public Guid AccountId { get; private set; }

    public Account Account { get; private set; } = null!;

    public string RefreshTokenHash { get; private set; } = string.Empty;

    public string? IpAddress { get; private set; }

    public string? UserAgent { get; private set; }

    public DateTimeOffset LastSeenAtUtc { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public DateTimeOffset? RevokedAtUtc { get; private set; }

    public string? RevokedReason { get; private set; }

    public static AccountSession Create(
        Guid id,
        Guid accountId,
        string refreshTokenHash,
        DateTimeOffset expiresAtUtc,
        string? ipAddress,
        string? userAgent,
        DateTimeOffset now)
    {
        if (expiresAtUtc <= now)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresAtUtc));
        }

        return new AccountSession(id, accountId, refreshTokenHash, expiresAtUtc, ipAddress, userAgent, now);
    }

    public bool IsActiveAt(DateTimeOffset now)
    {
        return RevokedAtUtc is null && ExpiresAtUtc > now;
    }

    public bool IsExpiredAt(DateTimeOffset now)
    {
        return ExpiresAtUtc <= now;
    }

    public void Rotate(string refreshTokenHash, DateTimeOffset expiresAtUtc, string? ipAddress, string? userAgent, DateTimeOffset now)
    {
        RefreshTokenHash = NormalizeRequired(refreshTokenHash, nameof(refreshTokenHash));
        ExpiresAtUtc = expiresAtUtc;
        IpAddress = NormalizeOptional(ipAddress, 64);
        UserAgent = NormalizeOptional(userAgent, 512);
        LastSeenAtUtc = now;
        MarkUpdated(now);
    }

    public void Touch(string? ipAddress, string? userAgent, DateTimeOffset now)
    {
        IpAddress = NormalizeOptional(ipAddress, 64);
        UserAgent = NormalizeOptional(userAgent, 512);
        LastSeenAtUtc = now;
        MarkUpdated(now);
    }

    public void Revoke(string reason, DateTimeOffset now)
    {
        if (RevokedAtUtc is not null)
        {
            return;
        }

        RevokedAtUtc = now;
        RevokedReason = NormalizeOptional(reason, 256);
        MarkUpdated(now);
    }

    private static string NormalizeRequired(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{paramName} is required.", paramName);
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength];
    }
}
