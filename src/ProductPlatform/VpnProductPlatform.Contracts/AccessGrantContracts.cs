namespace VpnProductPlatform.Contracts;

public sealed record AccessGrantResponse(
    Guid AccessGrantId,
    Guid DeviceId,
    string DeviceName,
    Guid? NodeId,
    Guid? ControlPlaneAccessId,
    string? PeerPublicKey,
    string? AllowedIps,
    string ConfigFormat,
    string Status,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    DateTimeOffset? RevokedAtUtc);

public sealed record IssueAccessGrantRequest(
    Guid DeviceId,
    Guid NodeId,
    string? ConfigFormat);

public sealed record IssuedAccessGrantResponse(
    Guid AccessGrantId,
    Guid DeviceId,
    string DeviceName,
    Guid NodeId,
    Guid? ControlPlaneAccessId,
    string? PeerPublicKey,
    string? AllowedIps,
    string ConfigFormat,
    string Status,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    DateTimeOffset? RevokedAtUtc,
    string ClientConfigFileName,
    string ClientConfig);

public sealed record IssuableNodeResponse(
    Guid NodeId,
    string Name,
    string Status,
    int ActiveSessions,
    int EnabledPeerCount);
