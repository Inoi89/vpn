namespace VpnProductPlatform.Contracts;

public sealed record AccessGrantResponse(
    Guid AccessGrantId,
    Guid DeviceId,
    string DeviceName,
    Guid? NodeId,
    string? PeerPublicKey,
    string ConfigFormat,
    string Status,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    DateTimeOffset? RevokedAtUtc);
