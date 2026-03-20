namespace VpnClient.Core.Interfaces;

public interface IKillSwitchService
{
    bool IsArmed { get; }

    Task ArmAsync(string endpoint, CancellationToken cancellationToken = default);

    Task DisarmAsync(CancellationToken cancellationToken = default);
}
