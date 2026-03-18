using System.Globalization;

namespace VpnClient.Infrastructure.Runtime;

public sealed record RuntimeWireGuardDump(
    string? Endpoint,
    DateTimeOffset? LatestHandshakeAtUtc,
    long ReceivedBytes,
    long SentBytes,
    bool IsTunnelActive,
    IReadOnlyList<string> Warnings)
{
    public static RuntimeWireGuardDump Parse(string dump, string adapterName)
    {
        var warnings = new List<string>();
        if (string.IsNullOrWhiteSpace(dump))
        {
            return new RuntimeWireGuardDump(null, null, 0, 0, false, ["No runtime data was returned by the AWG/WireGuard CLI."]);
        }

        var peerRows = dump.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(SplitColumns)
            .Where(columns => columns.Length >= 8)
            .ToArray();

        if (peerRows.Length < 2)
        {
            return new RuntimeWireGuardDump(null, null, 0, 0, false, ["No peer rows were returned by the AWG/WireGuard CLI."]);
        }

        var peer = peerRows[1];
        var endpoint = peer[2];
        var handshake = ParseHandshake(peer[4]);
        var receivedBytes = ParseLong(peer[5]);
        var sentBytes = ParseLong(peer[6]);
        var active = handshake is not null || receivedBytes > 0 || sentBytes > 0;

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            warnings.Add($"Adapter '{adapterName}' returned a peer row without endpoint information.");
        }

        return new RuntimeWireGuardDump(endpoint, handshake, receivedBytes, sentBytes, active, warnings);
    }

    private static DateTimeOffset? ParseHandshake(string raw)
    {
        if (string.Equals(raw, "never", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixSeconds) && unixSeconds > 0
            ? DateTimeOffset.FromUnixTimeSeconds(unixSeconds)
            : null;
    }

    private static long ParseLong(string raw)
    {
        return long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
    }

    private static string[] SplitColumns(string line)
    {
        if (line.Contains('\t'))
        {
            return line.Split('\t', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        return line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
