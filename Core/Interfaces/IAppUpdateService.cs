using VpnClient.Core.Models.Updates;

namespace VpnClient.Core.Interfaces;

public interface IAppUpdateService
{
    AppUpdateState CurrentState { get; }

    event Action<AppUpdateState>? StateChanged;

    Task<AppUpdateState> CheckForUpdatesAsync(CancellationToken cancellationToken = default);

    Task<AppUpdateState> PrepareUpdateAsync(CancellationToken cancellationToken = default);

    Task<AppUpdateState> LaunchPreparedUpdateAsync(CancellationToken cancellationToken = default);
}
