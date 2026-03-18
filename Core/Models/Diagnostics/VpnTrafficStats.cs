namespace VpnClient.Core.Models.Diagnostics;

public sealed record VpnTrafficStats
{
    public DateTimeOffset ObservedAtUtc { get; init; }
    public DateTimeOffset? LastHandshakeUtc { get; init; }
    public long TotalBytesReceived { get; init; }
    public long TotalBytesSent { get; init; }
    public int PeerCount { get; init; }

    public TimeSpan? HandshakeAge => LastHandshakeUtc is null
        ? null
        : ObservedAtUtc - LastHandshakeUtc.Value;
}
