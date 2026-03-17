using VpnControlPlane.Domain.Common;
using VpnControlPlane.Domain.Enums;

namespace VpnControlPlane.Domain.Entities;

public sealed class PeerConfig : AuditableEntity
{
    private PeerConfig()
    {
    }

    private PeerConfig(
        Guid id,
        Guid nodeId,
        Guid userId,
        string displayName,
        string publicKey,
        ProtocolFlavor protocolFlavor,
        string allowedIps,
        string? metadataJson,
        int revision,
        DateTimeOffset lastSyncedAtUtc,
        DateTimeOffset now)
    {
        Id = id;
        NodeId = nodeId;
        UserId = userId;
        DisplayName = displayName;
        PublicKey = publicKey;
        ProtocolFlavor = protocolFlavor;
        AllowedIps = allowedIps;
        MetadataJson = metadataJson;
        Revision = revision;
        LastSyncedAtUtc = lastSyncedAtUtc;
        IsEnabled = true;
        MarkCreated(now);
    }

    public Guid NodeId { get; private set; }

    public Node Node { get; private set; } = null!;

    public Guid UserId { get; private set; }

    public VpnUser User { get; private set; } = null!;

    public string DisplayName { get; private set; } = string.Empty;

    public string PublicKey { get; private set; } = string.Empty;

    public ProtocolFlavor ProtocolFlavor { get; private set; }

    public string AllowedIps { get; private set; } = string.Empty;

    public string? MetadataJson { get; private set; }

    public int Revision { get; private set; }

    public DateTimeOffset LastSyncedAtUtc { get; private set; }

    public bool IsEnabled { get; private set; }

    public static PeerConfig Create(
        Guid id,
        Guid nodeId,
        Guid userId,
        string displayName,
        string publicKey,
        ProtocolFlavor protocolFlavor,
        string allowedIps,
        string? metadataJson,
        int revision,
        DateTimeOffset lastSyncedAtUtc,
        DateTimeOffset now)
    {
        return new PeerConfig(
            id,
            nodeId,
            userId,
            displayName.Trim(),
            publicKey.Trim(),
            protocolFlavor,
            allowedIps.Trim(),
            string.IsNullOrWhiteSpace(metadataJson) ? null : metadataJson.Trim(),
            revision,
            lastSyncedAtUtc,
            now);
    }

    public void Refresh(
        Guid userId,
        string displayName,
        ProtocolFlavor protocolFlavor,
        string allowedIps,
        string? metadataJson,
        int revision,
        DateTimeOffset lastSyncedAtUtc,
        DateTimeOffset now)
    {
        UserId = userId;
        DisplayName = displayName.Trim();
        ProtocolFlavor = protocolFlavor;
        AllowedIps = allowedIps.Trim();
        MetadataJson = string.IsNullOrWhiteSpace(metadataJson) ? null : metadataJson.Trim();
        Revision = revision;
        LastSyncedAtUtc = lastSyncedAtUtc;
        IsEnabled = true;
        MarkUpdated(now);
    }

    public void Disable(DateTimeOffset lastSyncedAtUtc, DateTimeOffset now)
    {
        LastSyncedAtUtc = lastSyncedAtUtc;
        IsEnabled = false;
        MarkUpdated(now);
    }
}
