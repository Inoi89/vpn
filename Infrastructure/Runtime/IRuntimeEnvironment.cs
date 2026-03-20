namespace VpnClient.Infrastructure.Runtime;

public interface IRuntimeEnvironment
{
    bool IsWindows { get; }

    bool IsMacOS { get; }
}

public sealed class DefaultRuntimeEnvironment : IRuntimeEnvironment
{
    public bool IsWindows => OperatingSystem.IsWindows();

    public bool IsMacOS => OperatingSystem.IsMacOS();
}
