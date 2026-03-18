using VpnClient.Core.Interfaces;
using VpnClient.Core.Models;
using VpnClient.Core.Models.Diagnostics;
using VpnClient.Infrastructure.Logging;

namespace VpnClient.Infrastructure.Diagnostics;

public sealed class VpnDiagnosticsService : IVpnDiagnosticsService
{
    private readonly IProfileRepository _profileRepository;
    private readonly IVpnRuntimeAdapter _runtimeAdapter;
    private readonly object _gate = new();
    private readonly List<ImportValidationError> _importValidationErrors = [];
    private readonly List<ConnectionLogEntry> _connectionLogs = [];

    public VpnDiagnosticsService(
        IProfileRepository profileRepository,
        IVpnRuntimeAdapter runtimeAdapter)
    {
        _profileRepository = profileRepository;
        _runtimeAdapter = runtimeAdapter;
    }

    public void RecordImportValidationError(Exception exception, string? sourcePath = null)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var error = new ImportValidationError
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            SourcePath = sourcePath,
            FileName = string.IsNullOrWhiteSpace(sourcePath) ? null : Path.GetFileName(sourcePath),
            Stage = MapImportStage(exception),
            Message = exception.Message,
            ExceptionType = exception.GetType().FullName,
            Details = exception.StackTrace
        };

        lock (_gate)
        {
            _importValidationErrors.Add(error);
            TrimToLimit(_importValidationErrors, 250);
        }

        FileLogWriter.Write(
            "diagnostics.import",
            Microsoft.Extensions.Logging.LogLevel.Warning,
            $"Import validation failed at {error.Stage}: {error.Message}",
            exception);
    }

    public void RecordConnectionLog(string message, DiagnosticsLogLevel level = DiagnosticsLogLevel.Information, string? category = null, string? source = null, DateTimeOffset? timestampUtc = null)
    {
        RecordConnectionLog(new ConnectionLogEntry
        {
            TimestampUtc = timestampUtc ?? DateTimeOffset.UtcNow,
            Level = level,
            Message = message,
            Category = category,
            Source = source
        });
    }

    public void RecordConnectionLog(LogEntry entry, DiagnosticsLogLevel level = DiagnosticsLogLevel.Information, string? category = null, string? source = null)
    {
        ArgumentNullException.ThrowIfNull(entry);

        RecordConnectionLog(new ConnectionLogEntry
        {
            TimestampUtc = new DateTimeOffset(entry.Timestamp),
            Level = level,
            Message = entry.Message,
            Category = category,
            Source = source
        });
    }

    public async Task<VpnDiagnosticsSnapshot> CaptureSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var state = await _runtimeAdapter.GetStatusAsync(cancellationToken);
        var profiles = await _profileRepository.LoadAsync(cancellationToken);
        var currentProfile = state.ProfileId is Guid profileId
            ? profiles.Profiles.FirstOrDefault(profile => profile.Id == profileId)
            : state.Status == RuntimeConnectionStatus.Disconnected
                ? profiles.Profiles.FirstOrDefault(profile => profile.Id == profiles.ActiveProfileId)
                : null;

        List<ImportValidationError> importErrors;
        List<ConnectionLogEntry> connectionLogs;

        lock (_gate)
        {
            importErrors = [.. _importValidationErrors];
            connectionLogs = [.. _connectionLogs];
        }

        var trafficStats = state.Status == RuntimeConnectionStatus.Disconnected && state.ReceivedBytes == 0 && state.SentBytes == 0
            ? null
            : new VpnTrafficStats
            {
                ObservedAtUtc = now,
                LastHandshakeUtc = state.LatestHandshakeAtUtc,
                TotalBytesReceived = state.ReceivedBytes,
                TotalBytesSent = state.SentBytes,
                PeerCount = state.TunnelActive ? 1 : 0
            };

        return new VpnDiagnosticsSnapshot
        {
            CapturedAtUtc = now,
            Platform = OperatingSystem.IsWindows() ? "Windows" : Environment.OSVersion.Platform.ToString(),
            InterfaceName = state.AdapterName,
            ConnectionState = state,
            CurrentProfile = currentProfile,
            TrafficStats = trafficStats,
            ConnectionLogs = connectionLogs,
            ImportValidationErrors = importErrors
        };
    }

    private void RecordConnectionLog(ConnectionLogEntry entry)
    {
        lock (_gate)
        {
            _connectionLogs.Add(entry);
            TrimToLimit(_connectionLogs, 500);
        }

        FileLogWriter.Write(
            string.IsNullOrWhiteSpace(entry.Source) ? "diagnostics.connection" : entry.Source!,
            MapLogLevel(entry.Level),
            entry.Message);
    }

    private static void TrimToLimit<T>(List<T> items, int limit)
    {
        while (items.Count > limit)
        {
            items.RemoveAt(0);
        }
    }

    private static ImportValidationStage MapImportStage(Exception exception)
    {
        if (exception is ArgumentException)
        {
            return ImportValidationStage.Path;
        }

        if (exception is FileNotFoundException)
        {
            return ImportValidationStage.File;
        }

        if (exception is InvalidOperationException invalidOperation)
        {
            var message = invalidOperation.Message;

            if (message.Contains("decompress", StringComparison.OrdinalIgnoreCase)
                || message.Contains("malformed", StringComparison.OrdinalIgnoreCase))
            {
                return ImportValidationStage.Decode;
            }

            if (message.Contains("not a valid WireGuard/AmneziaWG config", StringComparison.OrdinalIgnoreCase))
            {
                return ImportValidationStage.Format;
            }

            if (message.Contains("does not contain a usable tunnel config", StringComparison.OrdinalIgnoreCase))
            {
                return ImportValidationStage.Parse;
            }
        }

        return ImportValidationStage.Unknown;
    }

    private static Microsoft.Extensions.Logging.LogLevel MapLogLevel(DiagnosticsLogLevel level) => level switch
    {
        DiagnosticsLogLevel.Trace => Microsoft.Extensions.Logging.LogLevel.Trace,
        DiagnosticsLogLevel.Debug => Microsoft.Extensions.Logging.LogLevel.Debug,
        DiagnosticsLogLevel.Information => Microsoft.Extensions.Logging.LogLevel.Information,
        DiagnosticsLogLevel.Warning => Microsoft.Extensions.Logging.LogLevel.Warning,
        DiagnosticsLogLevel.Error => Microsoft.Extensions.Logging.LogLevel.Error,
        DiagnosticsLogLevel.Critical => Microsoft.Extensions.Logging.LogLevel.Critical,
        _ => Microsoft.Extensions.Logging.LogLevel.Information
    };
}
