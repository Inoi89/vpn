using VpnClient.Core.Interfaces;
using VpnClient.Core.Models;
using VpnClient.Infrastructure.Runtime;

namespace VpnClient.UI.Platform;

internal sealed class UnsupportedVpnRuntimeAdapter : IVpnRuntimeAdapter
{
    private readonly string _platformName;
    private readonly string _reason;
    private ConnectionState _currentState;

    public UnsupportedVpnRuntimeAdapter(IRuntimeEnvironment environment)
    {
        _platformName = environment.IsWindows
            ? "Windows"
            : environment.IsMacOS
                ? "macOS"
                : "Platform";
        _reason = "VPN runtime is not available on this platform yet.";
        _currentState = ConnectionState.Unsupported(_platformName, _reason);
    }

    public ConnectionState CurrentState => _currentState;

    public event Action<ConnectionState>? StateChanged;

    public Task<ConnectionState> ConnectAsync(ImportedServerProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return Task.FromResult(UpdateState(ConnectionState.Unsupported(_platformName, _reason)));
    }

    public Task<ConnectionState> DisconnectAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(UpdateState(ConnectionState.Unsupported(_platformName, _reason)));
    }

    public Task<ConnectionState> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_currentState);
    }

    public Task<ConnectionState> TryRestoreAsync(IReadOnlyList<ImportedServerProfile> profiles, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_currentState);
    }

    private ConnectionState UpdateState(ConnectionState state)
    {
        _currentState = state;
        StateChanged?.Invoke(state);
        return state;
    }
}
