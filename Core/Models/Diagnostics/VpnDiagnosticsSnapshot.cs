using VpnClient.Core.Models;

namespace VpnClient.Core.Models.Diagnostics;

public sealed record VpnDiagnosticsSnapshot
{
    public DateTimeOffset CapturedAtUtc { get; init; }
    public string Platform { get; init; } = Environment.OSVersion.Platform.ToString();
    public string InterfaceName { get; init; } = "VpnClient";
    public ConnectionState ConnectionState { get; init; } = ConnectionState.Disconnected("VpnClient");
    public ImportedServerProfile? CurrentProfile { get; init; }
    public VpnTrafficStats? TrafficStats { get; init; }
    public IReadOnlyList<ConnectionLogEntry> ConnectionLogs { get; init; } = [];
    public IReadOnlyList<ImportValidationError> ImportValidationErrors { get; init; } = [];
}
