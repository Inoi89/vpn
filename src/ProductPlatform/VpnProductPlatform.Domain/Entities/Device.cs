using VpnProductPlatform.Domain.Common;
using VpnProductPlatform.Domain.Enums;

namespace VpnProductPlatform.Domain.Entities;

public sealed class Device : AuditableEntity
{
    private Device()
    {
    }

    private Device(
        Guid id,
        Guid accountId,
        string deviceName,
        string platform,
        string fingerprint,
        string? clientVersion,
        DateTimeOffset now)
    {
        Id = id;
        AccountId = accountId;
        DeviceName = NormalizeRequired(deviceName, nameof(deviceName));
        Platform = NormalizeRequired(platform, nameof(platform));
        Fingerprint = NormalizeRequired(fingerprint, nameof(fingerprint));
        ClientVersion = string.IsNullOrWhiteSpace(clientVersion) ? null : clientVersion.Trim();
        Status = DeviceStatus.Active;
        LastSeenAtUtc = now;
        MarkCreated(now);
    }

    public Guid AccountId { get; private set; }

    public Account Account { get; private set; } = null!;

    public string DeviceName { get; private set; } = string.Empty;

    public string Platform { get; private set; } = string.Empty;

    public string Fingerprint { get; private set; } = string.Empty;

    public string? ClientVersion { get; private set; }

    public DeviceStatus Status { get; private set; }

    public DateTimeOffset LastSeenAtUtc { get; private set; }

    public static Device Create(
        Guid id,
        Guid accountId,
        string deviceName,
        string platform,
        string fingerprint,
        string? clientVersion,
        DateTimeOffset now)
    {
        return new Device(id, accountId, deviceName, platform, fingerprint, clientVersion, now);
    }

    public void Touch(string deviceName, string platform, string? clientVersion, DateTimeOffset now)
    {
        DeviceName = NormalizeRequired(deviceName, nameof(deviceName));
        Platform = NormalizeRequired(platform, nameof(platform));
        ClientVersion = string.IsNullOrWhiteSpace(clientVersion) ? null : clientVersion.Trim();
        LastSeenAtUtc = now;
        Status = DeviceStatus.Active;
        MarkUpdated(now);
    }

    public void Revoke(DateTimeOffset now)
    {
        Status = DeviceStatus.Revoked;
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
}
