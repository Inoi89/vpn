using System.Text.Json;
using VpnControlPlane.Contracts.Nodes;
using VpnNodeAgent.Abstractions;

namespace VpnNodeAgent.Services;

public sealed class WireGuardConfigParser : IWireGuardConfigParser
{
    private static readonly string[] StandardWireGuardKeys =
    [
        "privatekey",
        "publickey",
        "allowedips",
        "endpoint",
        "presharedkey",
        "persistentkeepalive",
        "address",
        "listenport",
        "dns",
        "mtu",
        "table"
    ];

    private static readonly string[] AmneziaKeys =
    [
        "jc",
        "jmin",
        "jmax",
        "s1",
        "s2",
        "h1",
        "h2",
        "h3",
        "h4"
    ];

    public async Task<IReadOnlyList<PeerConfigSnapshot>> ParseAsync(IReadOnlyList<string> configFiles, CancellationToken cancellationToken)
    {
        var snapshots = new List<PeerConfigSnapshot>();

        foreach (var filePath in configFiles)
        {
            var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
            ParseFile(filePath, lines, snapshots);
        }

        return snapshots;
    }

    private static void ParseFile(string filePath, IReadOnlyList<string> lines, ICollection<PeerConfigSnapshot> output)
    {
        var sectionName = string.Empty;
        Dictionary<string, string>? peerProperties = null;
        Dictionary<string, string>? peerMetadata = null;

        void FlushSection()
        {
            if (!string.Equals(sectionName, "Peer", StringComparison.OrdinalIgnoreCase)
                || peerProperties is null
                || !peerProperties.TryGetValue("PublicKey", out var publicKey)
                || string.IsNullOrWhiteSpace(publicKey))
            {
                return;
            }

            var normalizedProperties = peerProperties.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
            var allowedIps = normalizedProperties.TryGetValue("AllowedIPs", out var allowedIpString)
                ? allowedIpString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : [];
            var protocol = DetectProtocol(normalizedProperties.Keys);
            var metadataJson = JsonSerializer.Serialize(new
            {
                sourceFile = Path.GetFileName(filePath),
                properties = normalizedProperties,
                metadata = peerMetadata ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            });
            var revision = unchecked((int)File.GetLastWriteTimeUtc(filePath).Ticks);

            output.Add(new PeerConfigSnapshot(
                publicKey.Trim(),
                TryGetMetadata(peerMetadata, "vpn-user-id"),
                TryGetMetadata(peerMetadata, "vpn-email"),
                TryGetMetadata(peerMetadata, "vpn-display-name"),
                protocol,
                allowedIps,
                metadataJson,
                revision));
        }

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.StartsWith('#') || line.StartsWith(';'))
            {
                var comment = line[1..].Trim();
                var separatorIndex = comment.IndexOf('=');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                var key = comment[..separatorIndex].Trim();
                var value = comment[(separatorIndex + 1)..].Trim();
                peerMetadata ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                peerMetadata[key] = value;
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                FlushSection();
                sectionName = line.Trim('[', ']');
                peerProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                continue;
            }

            var parts = line.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || peerProperties is null)
            {
                continue;
            }

            peerProperties[parts[0]] = parts[1];
        }

        FlushSection();
    }

    private static string DetectProtocol(IEnumerable<string> keys)
    {
        var normalizedKeys = keys.Select(x => x.Trim().ToLowerInvariant()).ToArray();
        return normalizedKeys.Any(x => AmneziaKeys.Contains(x, StringComparer.OrdinalIgnoreCase))
            || normalizedKeys.Any(x => !StandardWireGuardKeys.Contains(x, StringComparer.OrdinalIgnoreCase))
            ? "amnezia-wireguard"
            : "wireguard";
    }

    private static string? TryGetMetadata(IReadOnlyDictionary<string, string>? metadata, string key)
    {
        if (metadata is null)
        {
            return null;
        }

        return metadata.TryGetValue(key, out var value) ? value : null;
    }
}
