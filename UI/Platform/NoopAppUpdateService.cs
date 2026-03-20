using VpnClient.Core.Interfaces;
using VpnClient.Core.Models.Updates;

namespace VpnClient.UI.Platform;

internal sealed class NoopAppUpdateService : IAppUpdateService
{
    private readonly AppUpdateState _state;

    public NoopAppUpdateService()
    {
        var version = typeof(NoopAppUpdateService).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        _state = AppUpdateState.Disabled(version, reason: "Updates are not available on this platform yet.");
    }

    public AppUpdateState CurrentState => _state;

    public event Action<AppUpdateState>? StateChanged
    {
        add { }
        remove { }
    }

    public Task<AppUpdateState> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_state);

    public Task<AppUpdateState> PrepareUpdateAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_state);

    public Task<AppUpdateState> LaunchPreparedUpdateAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_state);
}
