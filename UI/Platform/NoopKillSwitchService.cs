using VpnClient.Core.Interfaces;

namespace VpnClient.UI.Platform;

internal sealed class NoopKillSwitchService : IKillSwitchService
{
    public bool IsArmed => false;

    public Task ArmAsync(string endpoint, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task DisarmAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
