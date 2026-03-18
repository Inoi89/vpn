using VpnClient.Core.Models.Diagnostics;

namespace VpnClient.Infrastructure.Diagnostics;

public static class WindowsWireGuardDumpParser
{
    public static VpnTrafficStats? Parse(string? dumpOutput, DateTimeOffset observedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(dumpOutput))
        {
            return null;
        }

        var peerCount = 0;
        var totalBytesReceived = 0L;
        var totalBytesSent = 0L;
        DateTimeOffset? latestHandshakeUtc = null;

        foreach (var rawLine in dumpOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var columns = line.Split('\t', StringSplitOptions.None);
            if (columns.Length < 8)
            {
                continue;
            }

            peerCount++;

            if (long.TryParse(columns[5], out var received))
            {
                totalBytesReceived += received;
            }

            if (long.TryParse(columns[6], out var sent))
            {
                totalBytesSent += sent;
            }

            if (TryParseHandshake(columns[4], out var handshakeUtc)
                && (latestHandshakeUtc is null || handshakeUtc > latestHandshakeUtc))
            {
                latestHandshakeUtc = handshakeUtc;
            }
        }

        if (peerCount == 0)
        {
            return null;
        }

        return new VpnTrafficStats
        {
            ObservedAtUtc = observedAtUtc,
            LastHandshakeUtc = latestHandshakeUtc,
            TotalBytesReceived = totalBytesReceived,
            TotalBytesSent = totalBytesSent,
            PeerCount = peerCount
        };
    }

    private static bool TryParseHandshake(string value, out DateTimeOffset timestampUtc)
    {
        timestampUtc = default;

        if (!long.TryParse(value, out var unixSeconds) || unixSeconds <= 0)
        {
            return false;
        }

        timestampUtc = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        return true;
    }
}
