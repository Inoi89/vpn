using VpnClient.Core.Interfaces;

namespace VpnClient.Infrastructure.Runtime;

public sealed class MacosNoOpKillSwitchService : IKillSwitchService
{
    public bool IsArmed => false;

    public Task ArmAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task DisarmAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
