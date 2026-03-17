using System.Text.Json;
using VpnControlPlane.Contracts.Nodes;
using VpnNodeAgent.Abstractions;

namespace VpnNodeAgent.Services;

public sealed class WireGuardConfigParser(IConfigFileReader fileReader) : IWireGuardConfigParser
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
        var snapshotBuilders = new Dictionary<string, PeerConfigSnapshotBuilder>(StringComparer.OrdinalIgnoreCase);

        foreach (var filePath in configFiles)
        {
            var lines = await fileReader.ReadAllLinesAsync(filePath, cancellationToken);
            var fileName = Path.GetFileName(filePath);

            if (string.Equals(fileName, "clientsTable", StringComparison.OrdinalIgnoreCase))
            {
                ParseClientsTable(filePath, string.Join('\n', lines), snapshotBuilders);
                continue;
            }

            ParseConfigFile(filePath, lines, snapshotBuilders);
        }

        return snapshotBuilders.Values
            .OrderBy(x => x.UserDisplayName ?? x.PublicKey, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.ToSnapshot())
            .ToList();
    }

    private static void ParseConfigFile(
        string filePath,
        IReadOnlyList<string> lines,
        IDictionary<string, PeerConfigSnapshotBuilder> output)
    {
        var sectionName = string.Empty;
        Dictionary<string, string>? interfaceProperties = null;
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
            var protocol = DetectProtocol(
                normalizedProperties.Keys.Concat(interfaceProperties?.Keys ?? Enumerable.Empty<string>()));
            var builder = GetOrCreate(output, publicKey.Trim());
            builder.Protocol = protocol;
            builder.UserExternalId ??= TryGetMetadata(peerMetadata, "vpn-user-id");
            builder.UserEmail ??= TryGetMetadata(peerMetadata, "vpn-email");
            builder.UserDisplayName ??= TryGetMetadata(peerMetadata, "vpn-display-name");

            foreach (var allowedIp in allowedIps)
            {
                builder.AllowedIps.Add(allowedIp);
            }

            builder.MetadataSources.Add(new
            {
                sourceFile = Path.GetFileName(filePath),
                interfaceProperties,
                peerProperties = normalizedProperties,
                metadata = peerMetadata ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            });
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
                peerProperties = string.Equals(sectionName, "Peer", StringComparison.OrdinalIgnoreCase)
                    ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    : null;
                interfaceProperties = string.Equals(sectionName, "Interface", StringComparison.OrdinalIgnoreCase)
                    ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    : interfaceProperties;
                peerMetadata = null;
                continue;
            }

            var parts = line.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
            {
                continue;
            }

            if (string.Equals(sectionName, "Peer", StringComparison.OrdinalIgnoreCase) && peerProperties is not null)
            {
                peerProperties[parts[0]] = parts[1];
            }
            else if (string.Equals(sectionName, "Interface", StringComparison.OrdinalIgnoreCase) && interfaceProperties is not null)
            {
                interfaceProperties[parts[0]] = parts[1];
            }
        }

        FlushSection();
    }

    private static void ParseClientsTable(
        string filePath,
        string rawJson,
        IDictionary<string, PeerConfigSnapshotBuilder> output)
    {
        using var document = JsonDocument.Parse(rawJson);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var client in document.RootElement.EnumerateArray())
        {
            if (!client.TryGetProperty("clientId", out var clientIdElement))
            {
                continue;
            }

            var publicKey = clientIdElement.GetString();
            if (string.IsNullOrWhiteSpace(publicKey))
            {
                continue;
            }

            var builder = GetOrCreate(output, publicKey.Trim());
            builder.Protocol = "amnezia-wireguard";
            builder.UserExternalId ??= publicKey.Trim();

            if (client.TryGetProperty("userData", out var userData) && userData.ValueKind == JsonValueKind.Object)
            {
                if (userData.TryGetProperty("clientName", out var clientName))
                {
                    builder.UserDisplayName ??= clientName.GetString();
                }

                if (userData.TryGetProperty("allowedIps", out var allowedIps))
                {
                    foreach (var allowedIp in allowedIps.GetString()?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [])
                    {
                        builder.AllowedIps.Add(allowedIp);
                    }
                }

                builder.MetadataSources.Add(new
                {
                    sourceFile = Path.GetFileName(filePath),
                    userData = JsonSerializer.Deserialize<object>(userData.GetRawText())
                });
            }
            else
            {
                builder.MetadataSources.Add(new
                {
                    sourceFile = Path.GetFileName(filePath),
                    raw = JsonSerializer.Deserialize<object>(client.GetRawText())
                });
            }
        }
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

    private static PeerConfigSnapshotBuilder GetOrCreate(
        IDictionary<string, PeerConfigSnapshotBuilder> output,
        string publicKey)
    {
        if (!output.TryGetValue(publicKey, out var builder))
        {
            builder = new PeerConfigSnapshotBuilder(publicKey);
            output[publicKey] = builder;
        }

        return builder;
    }

    private sealed class PeerConfigSnapshotBuilder(string publicKey)
    {
        public string PublicKey { get; } = publicKey;

        public string? UserExternalId { get; set; }

        public string? UserEmail { get; set; }

        public string? UserDisplayName { get; set; }

        public string Protocol { get; set; } = "wireguard";

        public HashSet<string> AllowedIps { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<object> MetadataSources { get; } = [];

        public PeerConfigSnapshot ToSnapshot()
        {
            var metadataJson = MetadataSources.Count == 0
                ? null
                : JsonSerializer.Serialize(new
                {
                    sources = MetadataSources
                });

            return new PeerConfigSnapshot(
                PublicKey,
                UserExternalId,
                UserEmail,
                UserDisplayName,
                Protocol,
                AllowedIps.ToList(),
                metadataJson,
                ComputeRevision());
        }

        private int ComputeRevision()
        {
            var payload = JsonSerializer.Serialize(new
            {
                PublicKey,
                UserExternalId,
                UserEmail,
                UserDisplayName,
                Protocol,
                AllowedIps = AllowedIps.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
                MetadataSources
            });

            return string.GetHashCode(payload, StringComparison.Ordinal);
        }
    }
}
