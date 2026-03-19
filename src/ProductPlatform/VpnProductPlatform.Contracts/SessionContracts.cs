namespace VpnProductPlatform.Contracts;

public sealed record SessionResponse(
    Guid SessionId,
    string Status,
    string? IpAddress,
    string? UserAgent,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastSeenAtUtc,
    DateTimeOffset ExpiresAtUtc,
    bool IsCurrent);
