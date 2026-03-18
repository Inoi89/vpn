using VpnClient.Core.Models.Diagnostics;
using VpnClient.Core.Models;

namespace VpnClient.Core.Interfaces;

public interface IVpnDiagnosticsService
{
    void RecordImportValidationError(Exception exception, string? sourcePath = null);
    void RecordConnectionLog(string message, DiagnosticsLogLevel level = DiagnosticsLogLevel.Information, string? category = null, string? source = null, DateTimeOffset? timestampUtc = null);
    void RecordConnectionLog(LogEntry entry, DiagnosticsLogLevel level = DiagnosticsLogLevel.Information, string? category = null, string? source = null);
    Task<VpnDiagnosticsSnapshot> CaptureSnapshotAsync(CancellationToken cancellationToken = default);
}
