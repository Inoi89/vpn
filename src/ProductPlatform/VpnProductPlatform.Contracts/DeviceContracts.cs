namespace VpnProductPlatform.Contracts;

public sealed record RegisterDeviceRequest(
    string DeviceName,
    string Platform,
    string Fingerprint,
    string? ClientVersion);

public sealed record DeviceResponse(
    Guid DeviceId,
    string DeviceName,
    string Platform,
    string? ClientVersion,
    string Fingerprint,
    string Status,
    DateTimeOffset LastSeenAtUtc);
