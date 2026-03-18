namespace VpnClient.Infrastructure.Runtime;

public interface IRuntimeEnvironment
{
    bool IsWindows { get; }
}

public sealed class DefaultRuntimeEnvironment : IRuntimeEnvironment
{
    public bool IsWindows => OperatingSystem.IsWindows();
}
