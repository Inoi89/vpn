namespace VpnClient.Core.Models.Diagnostics;

public sealed record ConnectionLogEntry
{
    public DateTimeOffset TimestampUtc { get; init; }
    public DiagnosticsLogLevel Level { get; init; } = DiagnosticsLogLevel.Information;
    public string Message { get; init; } = string.Empty;
    public string? Category { get; init; }
    public string? Source { get; init; }
}
