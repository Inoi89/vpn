namespace VpnClient.Core.Models;

public sealed record ClientSettings(
    bool AutoConnectEnabled = true,
    bool KillSwitchEnabled = false,
    bool NotificationsEnabled = true,
    bool LaunchToTrayEnabled = true);
