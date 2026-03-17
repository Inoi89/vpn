using VpnControlPlane.Domain.Common;
using VpnControlPlane.Domain.Enums;

namespace VpnControlPlane.Domain.Entities;

public sealed class Session : AuditableEntity
{
    private Session()
    {
    }

    private Session(
        Guid id,
        Guid nodeId,
        Guid userId,
        Guid? peerConfigId,
        string peerPublicKey,
        string? endpoint,
        DateTimeOffset? latestHandshakeAtUtc,
        DateTimeOffset observedAtUtc,
        long rxBytes,
        long txBytes,
        DateTimeOffset now)
    {
        Id = id;
        NodeId = nodeId;
        UserId = userId;
        PeerConfigId = peerConfigId;
        PeerPublicKey = peerPublicKey;
        Endpoint = endpoint;
        LastHandshakeAtUtc = latestHandshakeAtUtc;
        ConnectedAtUtc = latestHandshakeAtUtc ?? observedAtUtc;
        LastObservedAtUtc = observedAtUtc;
        LastRxBytes = rxBytes;
        LastTxBytes = txBytes;
        State = SessionState.Active;
        MarkCreated(now);
    }

    public Guid NodeId { get; private set; }

    public Node Node { get; private set; } = null!;

    public Guid UserId { get; private set; }

    public VpnUser User { get; private set; } = null!;

    public Guid? PeerConfigId { get; private set; }

    public PeerConfig? PeerConfig { get; private set; }

    public string PeerPublicKey { get; private set; } = string.Empty;

    public string? Endpoint { get; private set; }

    public SessionState State { get; private set; }

    public DateTimeOffset? ConnectedAtUtc { get; private set; }

    public DateTimeOffset? LastHandshakeAtUtc { get; private set; }

    public DateTimeOffset LastObservedAtUtc { get; private set; }

    public long LastRxBytes { get; private set; }

    public long LastTxBytes { get; private set; }

    public static Session Start(
        Guid id,
        Guid nodeId,
        Guid userId,
        Guid? peerConfigId,
        string peerPublicKey,
        string? endpoint,
        DateTimeOffset? latestHandshakeAtUtc,
        DateTimeOffset observedAtUtc,
        long rxBytes,
        long txBytes,
        DateTimeOffset now)
    {
        return new Session(
            id,
            nodeId,
            userId,
            peerConfigId,
            peerPublicKey.Trim(),
            string.IsNullOrWhiteSpace(endpoint) ? null : endpoint.Trim(),
            latestHandshakeAtUtc,
            observedAtUtc,
            rxBytes,
            txBytes,
            now);
    }

    public void Observe(
        Guid userId,
        Guid? peerConfigId,
        string? endpoint,
        DateTimeOffset? latestHandshakeAtUtc,
        bool isActive,
        DateTimeOffset observedAtUtc,
        long rxBytes,
        long txBytes,
        DateTimeOffset now)
    {
        UserId = userId;
        PeerConfigId = peerConfigId;
        Endpoint = string.IsNullOrWhiteSpace(endpoint) ? null : endpoint.Trim();
        LastHandshakeAtUtc = latestHandshakeAtUtc;
        LastObservedAtUtc = observedAtUtc;
        LastRxBytes = rxBytes;
        LastTxBytes = txBytes;
        State = isActive ? SessionState.Active : SessionState.Disconnected;

        if (State == SessionState.Active && ConnectedAtUtc is null)
        {
            ConnectedAtUtc = latestHandshakeAtUtc ?? observedAtUtc;
        }

        MarkUpdated(now);
    }

    public void Disconnect(DateTimeOffset observedAtUtc, DateTimeOffset now)
    {
        State = SessionState.Disconnected;
        LastObservedAtUtc = observedAtUtc;
        MarkUpdated(now);
    }
}
