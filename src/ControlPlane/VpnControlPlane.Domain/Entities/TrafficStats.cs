using VpnControlPlane.Domain.Common;

namespace VpnControlPlane.Domain.Entities;

public sealed class TrafficStats : AuditableEntity
{
    private TrafficStats()
    {
    }

    private TrafficStats(
        Guid id,
        Guid nodeId,
        Guid userId,
        Guid? sessionId,
        Guid? peerConfigId,
        long rxBytes,
        long txBytes,
        DateTimeOffset capturedAtUtc,
        DateTimeOffset now)
    {
        Id = id;
        NodeId = nodeId;
        UserId = userId;
        SessionId = sessionId;
        PeerConfigId = peerConfigId;
        RxBytes = rxBytes;
        TxBytes = txBytes;
        CapturedAtUtc = capturedAtUtc;
        MarkCreated(now);
    }

    public Guid NodeId { get; private set; }

    public Node Node { get; private set; } = null!;

    public Guid UserId { get; private set; }

    public VpnUser User { get; private set; } = null!;

    public Guid? SessionId { get; private set; }

    public Session? Session { get; private set; }

    public Guid? PeerConfigId { get; private set; }

    public PeerConfig? PeerConfig { get; private set; }

    public long RxBytes { get; private set; }

    public long TxBytes { get; private set; }

    public DateTimeOffset CapturedAtUtc { get; private set; }

    public static TrafficStats Capture(
        Guid id,
        Guid nodeId,
        Guid userId,
        Guid? sessionId,
        Guid? peerConfigId,
        long rxBytes,
        long txBytes,
        DateTimeOffset capturedAtUtc,
        DateTimeOffset now)
    {
        return new TrafficStats(
            id,
            nodeId,
            userId,
            sessionId,
            peerConfigId,
            rxBytes,
            txBytes,
            capturedAtUtc,
            now);
    }
}
