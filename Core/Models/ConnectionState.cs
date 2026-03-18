using System.Linq;

namespace VpnClient.Core.Models;

public enum RuntimeConnectionStatus
{
    Disconnected,
    Connecting,
    Connected,
    Disconnecting,
    Degraded,
    Failed,
    Unsupported
}

public sealed record ConnectionState
{
    public RuntimeConnectionStatus Status { get; init; }

    public string AdapterName { get; init; } = "VpnClient";

    public Guid? ProfileId { get; init; }

    public string? ProfileName { get; init; }

    public string? Endpoint { get; init; }

    public string? Address { get; init; }

    public IReadOnlyList<string> DnsServers { get; init; } = Array.Empty<string>();

    public int? Mtu { get; init; }

    public IReadOnlyList<string> AllowedIps { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Routes { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    public string? LastError { get; init; }

    public DateTimeOffset UpdatedAtUtc { get; init; }

    public DateTimeOffset? LatestHandshakeAtUtc { get; init; }

    public long ReceivedBytes { get; init; }

    public long SentBytes { get; init; }

    public bool AdapterPresent { get; init; }

    public bool TunnelActive { get; init; }

    public bool IsWindowsFirst { get; init; } = true;

    public bool UsesSetConf { get; init; }

    public string? PrimaryDns => DnsServers.FirstOrDefault();

    public static ConnectionState Disconnected(string adapterName) => new()
    {
        Status = RuntimeConnectionStatus.Disconnected,
        AdapterName = adapterName,
        UpdatedAtUtc = DateTimeOffset.UtcNow
    };

    public static ConnectionState Unsupported(string adapterName, string reason) => new()
    {
        Status = RuntimeConnectionStatus.Unsupported,
        AdapterName = adapterName,
        LastError = reason,
        Warnings = new[] { reason },
        UpdatedAtUtc = DateTimeOffset.UtcNow
    };

    public ConnectionState WithWarnings(params string[] warnings)
    {
        var merged = Warnings.Concat(warnings.Where(warning => !string.IsNullOrWhiteSpace(warning)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return this with
        {
            Warnings = merged,
            Status = merged.Length > 0 && Status == RuntimeConnectionStatus.Connected
                ? RuntimeConnectionStatus.Degraded
                : Status,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
    }
}
