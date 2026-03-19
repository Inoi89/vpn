using VpnProductPlatform.Domain.Common;
using VpnProductPlatform.Domain.Enums;

namespace VpnProductPlatform.Domain.Entities;

public sealed class AccessGrant : AuditableEntity
{
    private AccessGrant()
    {
    }

    private AccessGrant(
        Guid id,
        Guid accountId,
        Guid deviceId,
        Guid? nodeId,
        string? peerPublicKey,
        string configFormat,
        DateTimeOffset issuedAtUtc,
        DateTimeOffset? expiresAtUtc,
        DateTimeOffset now)
    {
        Id = id;
        AccountId = accountId;
        DeviceId = deviceId;
        NodeId = nodeId;
        PeerPublicKey = string.IsNullOrWhiteSpace(peerPublicKey) ? null : peerPublicKey.Trim();
        ConfigFormat = NormalizeRequired(configFormat, nameof(configFormat));
        IssuedAtUtc = issuedAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        Status = AccessGrantStatus.Pending;
        MarkCreated(now);
    }

    public Guid AccountId { get; private set; }

    public Account Account { get; private set; } = null!;

    public Guid DeviceId { get; private set; }

    public Device Device { get; private set; } = null!;

    public Guid? NodeId { get; private set; }

    public string? PeerPublicKey { get; private set; }

    public string ConfigFormat { get; private set; } = string.Empty;

    public AccessGrantStatus Status { get; private set; }

    public DateTimeOffset IssuedAtUtc { get; private set; }

    public DateTimeOffset? ExpiresAtUtc { get; private set; }

    public DateTimeOffset? RevokedAtUtc { get; private set; }

    public static AccessGrant Create(
        Guid id,
        Guid accountId,
        Guid deviceId,
        Guid? nodeId,
        string? peerPublicKey,
        string configFormat,
        DateTimeOffset issuedAtUtc,
        DateTimeOffset? expiresAtUtc,
        DateTimeOffset now)
    {
        return new AccessGrant(id, accountId, deviceId, nodeId, peerPublicKey, configFormat, issuedAtUtc, expiresAtUtc, now);
    }

    public void Activate(Guid? nodeId, string? peerPublicKey, DateTimeOffset now)
    {
        NodeId = nodeId;
        PeerPublicKey = string.IsNullOrWhiteSpace(peerPublicKey) ? null : peerPublicKey.Trim();
        Status = AccessGrantStatus.Active;
        MarkUpdated(now);
    }

    public void Revoke(DateTimeOffset now)
    {
        Status = AccessGrantStatus.Revoked;
        RevokedAtUtc = now;
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
