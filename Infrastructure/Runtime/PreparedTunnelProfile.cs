namespace VpnClient.Infrastructure.Runtime;

public sealed record PreparedTunnelProfile(
    Guid ProfileId,
    string ProfileName,
    string TunnelName,
    string ConfigPath);
