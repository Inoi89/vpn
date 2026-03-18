using VpnClient.Core.Interfaces;
using VpnClient.Core.Models;

namespace VpnClient.Infrastructure.Runtime;

public sealed class HybridVpnRuntimeAdapter : IVpnRuntimeAdapter
{
    private readonly BundledAmneziaRuntimeAdapter _bundledAdapter;
    private readonly AmneziaDaemonRuntimeAdapter _daemonAdapter;
    private readonly WindowsFirstVpnRuntimeAdapter _fallbackAdapter;
    private readonly IAmneziaDaemonTransport _daemonTransport;
    private ActiveBackend _activeBackend = ActiveBackend.None;
    private ConnectionState _currentState = ConnectionState.Disconnected("VpnClient");

    public HybridVpnRuntimeAdapter(
        BundledAmneziaRuntimeAdapter bundledAdapter,
        AmneziaDaemonRuntimeAdapter daemonAdapter,
        WindowsFirstVpnRuntimeAdapter fallbackAdapter,
        IAmneziaDaemonTransport daemonTransport)
    {
        _bundledAdapter = bundledAdapter;
        _daemonAdapter = daemonAdapter;
        _fallbackAdapter = fallbackAdapter;
        _daemonTransport = daemonTransport;

        _bundledAdapter.StateChanged += state =>
        {
            if (_activeBackend == ActiveBackend.Bundled)
            {
                UpdateState(state);
            }
        };

        _daemonAdapter.StateChanged += state =>
        {
            if (_activeBackend == ActiveBackend.Daemon)
            {
                UpdateState(state);
            }
        };

        _fallbackAdapter.StateChanged += state =>
        {
            if (_activeBackend == ActiveBackend.Fallback)
            {
                UpdateState(state);
            }
        };
    }

    public ConnectionState CurrentState => _currentState;

    public event Action<ConnectionState>? StateChanged;

    public async Task<ConnectionState> ConnectAsync(ImportedServerProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var bundledState = await _bundledAdapter.ConnectAsync(profile, cancellationToken);
        if (bundledState.Status != RuntimeConnectionStatus.Unsupported)
        {
            _activeBackend = ActiveBackend.Bundled;
            return UpdateState(bundledState);
        }

        if (await _daemonTransport.IsAvailableAsync(cancellationToken))
        {
            _activeBackend = ActiveBackend.Daemon;
            return UpdateState(await _daemonAdapter.ConnectAsync(profile, cancellationToken));
        }

        _activeBackend = ActiveBackend.Fallback;
        var state = await _fallbackAdapter.ConnectAsync(profile, cancellationToken);
        if (!state.Warnings.Any(warning => warning.Contains("Amnezia daemon", StringComparison.OrdinalIgnoreCase)))
        {
            state = state with
            {
                Warnings = state.Warnings.Concat(new[]
                {
                    "Amnezia daemon is unavailable. The client is running in Windows fallback mode, which may not fully reproduce Amnezia runtime semantics."
                }).ToArray()
            };
        }

        return UpdateState(state);
    }

    public async Task<ConnectionState> TryRestoreAsync(IReadOnlyList<ImportedServerProfile> profiles, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profiles);

        if (_activeBackend != ActiveBackend.None)
        {
            return await GetStatusAsync(cancellationToken);
        }

        var bundledState = await _bundledAdapter.TryRestoreAsync(profiles, cancellationToken);
        if (IsRestoredState(bundledState))
        {
            _activeBackend = ActiveBackend.Bundled;
            return UpdateState(bundledState);
        }

        var daemonState = await _daemonAdapter.TryRestoreAsync(profiles, cancellationToken);
        if (IsRestoredState(daemonState))
        {
            _activeBackend = ActiveBackend.Daemon;
            return UpdateState(daemonState);
        }

        var fallbackState = await _fallbackAdapter.TryRestoreAsync(profiles, cancellationToken);
        if (IsRestoredState(fallbackState))
        {
            _activeBackend = ActiveBackend.Fallback;
            return UpdateState(fallbackState);
        }

        _activeBackend = ActiveBackend.None;
        return UpdateState(ConnectionState.Disconnected("VpnClient"));
    }

    public async Task<ConnectionState> DisconnectAsync(CancellationToken cancellationToken = default)
    {
        return _activeBackend switch
        {
            ActiveBackend.Bundled => UpdateState(await _bundledAdapter.DisconnectAsync(cancellationToken)),
            ActiveBackend.Daemon => UpdateState(await _daemonAdapter.DisconnectAsync(cancellationToken)),
            ActiveBackend.Fallback => UpdateState(await _fallbackAdapter.DisconnectAsync(cancellationToken)),
            _ => UpdateState(ConnectionState.Disconnected("VpnClient"))
        };
    }

    public async Task<ConnectionState> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        return _activeBackend switch
        {
            ActiveBackend.Bundled => UpdateState(await _bundledAdapter.GetStatusAsync(cancellationToken)),
            ActiveBackend.Daemon => UpdateState(await _daemonAdapter.GetStatusAsync(cancellationToken)),
            ActiveBackend.Fallback => UpdateState(await _fallbackAdapter.GetStatusAsync(cancellationToken)),
            _ => UpdateState(ConnectionState.Disconnected("VpnClient"))
        };
    }

    private ConnectionState UpdateState(ConnectionState state)
    {
        _currentState = state;
        StateChanged?.Invoke(state);
        return state;
    }

    private static bool IsRestoredState(ConnectionState state)
    {
        return state.Status != RuntimeConnectionStatus.Unsupported
               && (state.AdapterPresent
                   || state.TunnelActive
                   || state.Status is RuntimeConnectionStatus.Connected or RuntimeConnectionStatus.Connecting or RuntimeConnectionStatus.Degraded);
    }

    private enum ActiveBackend
    {
        None,
        Bundled,
        Daemon,
        Fallback
    }
}
