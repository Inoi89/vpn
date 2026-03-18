using VpnClient.Core.Interfaces;
using VpnClient.Core.Models.Updates;

namespace VpnClient.Application.Updates;

public sealed class LaunchPreparedAppUpdateUseCase
{
    private readonly IAppUpdateService _appUpdateService;

    public LaunchPreparedAppUpdateUseCase(IAppUpdateService appUpdateService)
    {
        _appUpdateService = appUpdateService;
    }

    public Task<AppUpdateState> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        return _appUpdateService.LaunchPreparedUpdateAsync(cancellationToken);
    }
}
