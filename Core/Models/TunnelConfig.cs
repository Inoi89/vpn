namespace VpnClient.Core.Models;

public sealed record TunnelConfig(
    TunnelConfigFormat Format,
    string RawConfig,
    IReadOnlyList<ConfigLine> Lines,
    IReadOnlyDictionary<string, string> InterfaceValues,
    IReadOnlyDictionary<string, string> PeerValues,
    IReadOnlyDictionary<string, string> AwgValues,
    string? Address,
    IReadOnlyList<string> DnsServers,
    string? Mtu,
    IReadOnlyList<string> AllowedIps,
    int? PersistentKeepalive,
    string? Endpoint,
    string? PublicKey,
    string? PresharedKey);
