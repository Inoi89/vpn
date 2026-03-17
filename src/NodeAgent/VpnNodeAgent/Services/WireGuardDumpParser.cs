using Microsoft.Extensions.Options;
using VpnNodeAgent.Abstractions;
using VpnNodeAgent.Configuration;
using VpnNodeAgent.Models;

namespace VpnNodeAgent.Services;

public sealed class WireGuardDumpParser(IOptions<AgentOptions> options) : IWireGuardDumpParser
{
    public IReadOnlyList<WireGuardPeerRuntime> Parse(string rawDump)
    {
        var peers = new List<WireGuardPeerRuntime>();
        var lines = rawDump.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var activityWindow = TimeSpan.FromSeconds(options.Value.ActiveHandshakeWindowSeconds);

        foreach (var line in lines)
        {
            var columns = line.Split('\t');
            if (columns.Length < 8)
            {
                continue;
            }

            if (columns.Length == 5 || !LooksLikePeerLine(columns))
            {
                continue;
            }

            var latestHandshake = ParseHandshake(columns[5]);
            var rxBytes = long.TryParse(columns[6], out var parsedRx) ? parsedRx : 0L;
            var txBytes = long.TryParse(columns[7], out var parsedTx) ? parsedTx : 0L;
            var isActive = latestHandshake.HasValue
                && DateTimeOffset.UtcNow - latestHandshake.Value <= activityWindow;

            peers.Add(new WireGuardPeerRuntime(
                columns[0],
                columns[1],
                columns[4],
                string.IsNullOrWhiteSpace(columns[3]) || columns[3] == "(none)" ? null : columns[3],
                latestHandshake,
                rxBytes,
                txBytes,
                isActive));
        }

        return peers;
    }

    private static bool LooksLikePeerLine(string[] columns)
    {
        var allowedIps = columns[4];
        if (string.IsNullOrWhiteSpace(allowedIps))
        {
            return false;
        }

        return allowedIps.Contains('/', StringComparison.Ordinal)
            || string.Equals(allowedIps, "(none)", StringComparison.OrdinalIgnoreCase);
    }

    private static DateTimeOffset? ParseHandshake(string value)
    {
        if (!long.TryParse(value, out var seconds) || seconds <= 0)
        {
            return null;
        }

        return DateTimeOffset.FromUnixTimeSeconds(seconds);
    }
}
