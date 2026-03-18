using Microsoft.Extensions.Logging;
using VpnClient.Core.Interfaces;
using VpnClient.Core.Models;

namespace VpnClient.Infrastructure.Runtime;

public sealed class BundledAmneziaRuntimeAdapter : IVpnRuntimeAdapter
{
    private const string AdapterName = "BundledAmneziaWG";

    private readonly IRuntimeCommandExecutor _commandExecutor;
    private readonly IRuntimeEnvironment _environment;
    private readonly IWindowsRuntimeAssetLocator _assetLocator;
    private readonly IAmneziaRuntimeConfigStore _configStore;
    private readonly ILogger<BundledAmneziaRuntimeAdapter> _logger;

    private ImportedServerProfile? _activeProfile;
    private PreparedTunnelProfile? _activePreparedProfile;
    private ConnectionState _currentState = ConnectionState.Disconnected(AdapterName);

    public BundledAmneziaRuntimeAdapter(
        IRuntimeCommandExecutor commandExecutor,
        IRuntimeEnvironment environment,
        IWindowsRuntimeAssetLocator assetLocator,
        IAmneziaRuntimeConfigStore configStore,
        ILogger<BundledAmneziaRuntimeAdapter> logger)
    {
        _commandExecutor = commandExecutor;
        _environment = environment;
        _assetLocator = assetLocator;
        _configStore = configStore;
        _logger = logger;
    }

    public ConnectionState CurrentState => _currentState;

    public event Action<ConnectionState>? StateChanged;

    public async Task<ConnectionState> ConnectAsync(ImportedServerProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (!_environment.IsWindows)
        {
            return UpdateState(ConnectionState.Unsupported(AdapterName, "Bundled AmneziaWG runtime is supported only on Windows."));
        }

        var runtimeAvailabilityError = GetRuntimeAvailabilityError();
        if (runtimeAvailabilityError is not null)
        {
            return UpdateState(ConnectionState.Unsupported(AdapterName, runtimeAvailabilityError));
        }

        var warnings = BuildWarnings(profile).ToArray();
        var preparedProfile = await _configStore.PrepareAsync(profile, cancellationToken);

        UpdateState(new ConnectionState
        {
            Status = RuntimeConnectionStatus.Connecting,
            AdapterName = AdapterName,
            ProfileId = profile.Id,
            ProfileName = profile.DisplayName,
            Endpoint = profile.Endpoint,
            Address = profile.Address,
            DnsServers = profile.DnsServers,
            Mtu = ParseNullableInt(profile.Mtu),
            AllowedIps = profile.AllowedIps,
            Routes = profile.AllowedIps,
            Warnings = warnings,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            AdapterPresent = false,
            TunnelActive = false,
            IsWindowsFirst = false,
            UsesSetConf = false
        });

        try
        {
            var installResult = await _commandExecutor.ExecuteAsync(
                _assetLocator.AmneziaWgExecutablePath,
                ["/installtunnelservice", preparedProfile.ConfigPath],
                cancellationToken);

            var attachedToExistingTunnel = LooksLikeAlreadyInstalledTunnel(installResult);
            if (installResult.ExitCode != 0 && !attachedToExistingTunnel)
            {
                return UpdateState(BuildFailureState(
                    profile,
                    warnings,
                    $"Bundled AmneziaWG failed to install the tunnel service. {GetCommandError(installResult, "amneziawg.exe /installtunnelservice failed.")}"));
            }

            _activeProfile = profile;
            _activePreparedProfile = preparedProfile;
            var effectiveWarnings = attachedToExistingTunnel
                ? warnings.Concat(new[]
                {
                    "The tunnel service was already installed. The client attached to the existing AmneziaWG runtime instance."
                }).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
                : warnings;

            for (var attempt = 0; attempt < 10; attempt++)
            {
                var state = await GetStatusInternalAsync(profile, preparedProfile, effectiveWarnings, cancellationToken);
                if (state.Status is RuntimeConnectionStatus.Connected or RuntimeConnectionStatus.Degraded
                    || state.AdapterPresent)
                {
                    return UpdateState(state);
                }

                await Task.Delay(TimeSpan.FromMilliseconds(350), cancellationToken);
            }

            return UpdateState(await GetStatusInternalAsync(profile, preparedProfile, effectiveWarnings, cancellationToken));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Bundled AmneziaWG runtime failed to connect.");
            return UpdateState(BuildFailureState(profile, warnings, exception.Message));
        }
    }

    public async Task<ConnectionState> DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (!_environment.IsWindows)
        {
            return UpdateState(ConnectionState.Unsupported(AdapterName, "Bundled AmneziaWG runtime is supported only on Windows."));
        }

        if (_activePreparedProfile is null)
        {
            return UpdateState(ConnectionState.Disconnected(AdapterName));
        }

        try
        {
            var result = await _commandExecutor.ExecuteAsync(
                _assetLocator.AmneziaWgExecutablePath,
                ["/uninstalltunnelservice", _activePreparedProfile.TunnelName],
                cancellationToken);

            if (result.ExitCode != 0 && !LooksLikeMissingTunnel(result))
            {
                return UpdateState((_currentState with
                {
                    Status = RuntimeConnectionStatus.Degraded,
                    LastError = GetCommandError(result, "Bundled AmneziaWG failed to uninstall the tunnel service."),
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                    AdapterPresent = false,
                    TunnelActive = false
                }).WithWarnings("Tunnel service removal reported an error."));
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Bundled AmneziaWG runtime failed to disconnect cleanly.");
            return UpdateState((_currentState with
            {
                Status = RuntimeConnectionStatus.Degraded,
                LastError = exception.Message,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                AdapterPresent = false,
                TunnelActive = false
            }).WithWarnings("Tunnel service removal reported an exception."));
        }
        finally
        {
            await _configStore.DeleteAsync(_activePreparedProfile, cancellationToken);
            _activePreparedProfile = null;
            _activeProfile = null;
        }

        return UpdateState(ConnectionState.Disconnected(AdapterName));
    }

    public async Task<ConnectionState> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        if (!_environment.IsWindows)
        {
            return UpdateState(ConnectionState.Unsupported(AdapterName, "Bundled AmneziaWG runtime is supported only on Windows."));
        }

        if (_activeProfile is null || _activePreparedProfile is null)
        {
            return UpdateState(ConnectionState.Disconnected(AdapterName));
        }

        var warnings = BuildWarnings(_activeProfile).ToArray();
        return UpdateState(await GetStatusInternalAsync(_activeProfile, _activePreparedProfile, warnings, cancellationToken));
    }

    public async Task<ConnectionState> TryRestoreAsync(IReadOnlyList<ImportedServerProfile> profiles, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profiles);

        if (!_environment.IsWindows)
        {
            return UpdateState(ConnectionState.Unsupported(AdapterName, "Bundled AmneziaWG runtime is supported only on Windows."));
        }

        var runtimeAvailabilityError = GetRuntimeAvailabilityError();
        if (runtimeAvailabilityError is not null)
        {
            return UpdateState(ConnectionState.Unsupported(AdapterName, runtimeAvailabilityError));
        }

        if (_activeProfile is not null && _activePreparedProfile is not null)
        {
            return await GetStatusAsync(cancellationToken);
        }

        foreach (var profile in profiles)
        {
            var preparedProfile = _configStore.Describe(profile);
            var warnings = BuildWarnings(profile).ToArray();
            var state = await GetStatusInternalAsync(profile, preparedProfile, warnings, cancellationToken);
            if (!LooksLikeRestorableTunnel(state))
            {
                continue;
            }

            _activeProfile = profile;
            _activePreparedProfile = preparedProfile;

            return UpdateState(state with
            {
                Warnings = state.Warnings.Concat(new[]
                {
                    "Restored an existing bundled AmneziaWG tunnel state from the local tunnel service."
                }).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
        }

        return UpdateState(ConnectionState.Disconnected(AdapterName));
    }

    private async Task<ConnectionState> GetStatusInternalAsync(
        ImportedServerProfile profile,
        PreparedTunnelProfile preparedProfile,
        IReadOnlyList<string> warnings,
        CancellationToken cancellationToken)
    {
        var showResult = await _commandExecutor.ExecuteAsync(
            _assetLocator.AwgExecutablePath,
            ["show", preparedProfile.TunnelName, "dump"],
            cancellationToken);

        if (showResult.ExitCode == 0)
        {
            var runtime = RuntimeWireGuardDump.Parse(showResult.StandardOutput, preparedProfile.TunnelName);
            var mergedWarnings = warnings.Concat(runtime.Warnings).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var hasTraffic = runtime.ReceivedBytes > 0 || runtime.SentBytes > 0;

            return new ConnectionState
            {
                Status = runtime.IsTunnelActive
                    ? RuntimeConnectionStatus.Connected
                    : hasTraffic
                        ? RuntimeConnectionStatus.Degraded
                        : RuntimeConnectionStatus.Connecting,
                AdapterName = AdapterName,
                ProfileId = profile.Id,
                ProfileName = profile.DisplayName,
                Endpoint = runtime.Endpoint ?? profile.Endpoint,
                Address = profile.Address,
                DnsServers = profile.DnsServers,
                Mtu = ParseNullableInt(profile.Mtu),
                AllowedIps = profile.AllowedIps,
                Routes = profile.AllowedIps,
                Warnings = mergedWarnings,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                LatestHandshakeAtUtc = runtime.LatestHandshakeAtUtc,
                ReceivedBytes = runtime.ReceivedBytes,
                SentBytes = runtime.SentBytes,
                AdapterPresent = true,
                TunnelActive = runtime.IsTunnelActive || hasTraffic,
                IsWindowsFirst = false,
                UsesSetConf = false
            };
        }

        var serviceState = await QueryServiceStateAsync(preparedProfile.TunnelName, cancellationToken);
        var mergedServiceWarnings = warnings.ToList();

        if (serviceState == TunnelServiceState.NotFound)
        {
            return new ConnectionState
            {
                Status = RuntimeConnectionStatus.Disconnected,
                AdapterName = AdapterName,
                ProfileId = profile.Id,
                ProfileName = profile.DisplayName,
                Endpoint = profile.Endpoint,
                Address = profile.Address,
                DnsServers = profile.DnsServers,
                Mtu = ParseNullableInt(profile.Mtu),
                AllowedIps = profile.AllowedIps,
                Routes = profile.AllowedIps,
                Warnings = mergedServiceWarnings,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                AdapterPresent = false,
                TunnelActive = false,
                IsWindowsFirst = false,
                UsesSetConf = false
            };
        }

        mergedServiceWarnings.Add(GetCommandError(showResult, "AWG runtime status probe did not return interface data yet."));

        return new ConnectionState
        {
            Status = serviceState is TunnelServiceState.Running or TunnelServiceState.StartPending
                ? RuntimeConnectionStatus.Connecting
                : RuntimeConnectionStatus.Degraded,
            AdapterName = AdapterName,
            ProfileId = profile.Id,
            ProfileName = profile.DisplayName,
            Endpoint = profile.Endpoint,
            Address = profile.Address,
            DnsServers = profile.DnsServers,
            Mtu = ParseNullableInt(profile.Mtu),
            AllowedIps = profile.AllowedIps,
            Routes = profile.AllowedIps,
            Warnings = mergedServiceWarnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            AdapterPresent = serviceState is TunnelServiceState.Running or TunnelServiceState.StartPending,
            TunnelActive = false,
            IsWindowsFirst = false,
            UsesSetConf = false
        };
    }

    private async Task<TunnelServiceState> QueryServiceStateAsync(string tunnelName, CancellationToken cancellationToken)
    {
        var serviceName = $"AmneziaWGTunnel${tunnelName}";
        var result = await _commandExecutor.ExecuteAsync("sc.exe", ["query", serviceName], cancellationToken);
        var output = $"{result.StandardOutput}\n{result.StandardError}";

        if (output.Contains("FAILED 1060", StringComparison.OrdinalIgnoreCase)
            || output.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
        {
            return TunnelServiceState.NotFound;
        }

        if (output.Contains("RUNNING", StringComparison.OrdinalIgnoreCase))
        {
            return TunnelServiceState.Running;
        }

        if (output.Contains("START_PENDING", StringComparison.OrdinalIgnoreCase))
        {
            return TunnelServiceState.StartPending;
        }

        if (output.Contains("STOPPED", StringComparison.OrdinalIgnoreCase))
        {
            return TunnelServiceState.Stopped;
        }

        return TunnelServiceState.Unknown;
    }

    private string? GetRuntimeAvailabilityError()
    {
        if (_assetLocator.HasBundledAmneziaWgExecutable
            && _assetLocator.HasBundledAwgExecutable
            && _assetLocator.HasBundledWintun)
        {
            return null;
        }

        return "Bundled AmneziaWG runtime is not available. Publish amneziawg.exe, awg.exe, and wintun.dll into runtime\\wireguard.";
    }

    private ConnectionState BuildFailureState(
        ImportedServerProfile profile,
        IReadOnlyList<string> warnings,
        string error)
    {
        return new ConnectionState
        {
            Status = RuntimeConnectionStatus.Failed,
            AdapterName = AdapterName,
            ProfileId = profile.Id,
            ProfileName = profile.DisplayName,
            Endpoint = profile.Endpoint,
            Address = profile.Address,
            DnsServers = profile.DnsServers,
            Mtu = ParseNullableInt(profile.Mtu),
            AllowedIps = profile.AllowedIps,
            Routes = profile.AllowedIps,
            Warnings = warnings,
            LastError = error,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            AdapterPresent = false,
            TunnelActive = false,
            IsWindowsFirst = false,
            UsesSetConf = false
        };
    }

    private static IEnumerable<string> BuildWarnings(ImportedServerProfile profile)
    {
        if (profile.HasAwgMetadata)
        {
            yield return "AWG metadata will be applied through the bundled AmneziaWG tunnel service.";
        }

        if (profile.DnsServers.Count == 0)
        {
            yield return "The imported profile does not define DNS servers.";
        }
    }

    private static int? ParseNullableInt(string? value)
    {
        return int.TryParse(value, out var parsed)
            ? parsed
            : null;
    }

    private static string GetCommandError(RuntimeCommandResult result, string fallbackMessage)
    {
        if (!string.IsNullOrWhiteSpace(result.StandardError))
        {
            return result.StandardError.Trim();
        }

        if (!string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            return result.StandardOutput.Trim();
        }

        return fallbackMessage;
    }

    private static bool LooksLikeMissingTunnel(RuntimeCommandResult result)
    {
        var text = $"{result.StandardOutput}\n{result.StandardError}";
        return text.Contains("1060", StringComparison.OrdinalIgnoreCase)
               || text.Contains("does not exist", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeAlreadyInstalledTunnel(RuntimeCommandResult result)
    {
        var text = $"{result.StandardOutput}\n{result.StandardError}";
        return text.Contains("already installed and running", StringComparison.OrdinalIgnoreCase)
               || text.Contains("tunnel already installed", StringComparison.OrdinalIgnoreCase)
               || text.Contains("already installed", StringComparison.OrdinalIgnoreCase)
               || text.Contains("already exists", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeRestorableTunnel(ConnectionState state)
    {
        return state.AdapterPresent
               || state.TunnelActive
               || state.LatestHandshakeAtUtc is not null
               || state.ReceivedBytes > 0
               || state.SentBytes > 0
               || state.Status == RuntimeConnectionStatus.Connecting;
    }

    private ConnectionState UpdateState(ConnectionState state)
    {
        _currentState = state;
        StateChanged?.Invoke(state);
        return state;
    }

    private enum TunnelServiceState
    {
        Unknown,
        NotFound,
        StartPending,
        Running,
        Stopped
    }
}
