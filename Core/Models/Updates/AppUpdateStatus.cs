namespace VpnClient.Core.Models.Updates;

public enum AppUpdateStatus
{
    Disabled,
    Idle,
    Checking,
    UpToDate,
    UpdateAvailable,
    Downloading,
    ReadyToInstall,
    Installing,
    Failed
}
