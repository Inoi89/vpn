namespace VpnNodeAgent.Models;

public sealed record WireGuardPeerRuntime(
    string InterfaceName,
    string PublicKey,
    string AllowedIps,
    string? Endpoint,
    DateTimeOffset? LatestHandshakeAtUtc,
    long RxBytes,
    long TxBytes,
    bool IsActive);
