namespace VpnClient.Core.Models.Updates;

public sealed record AppUpdateState
{
    public AppUpdateStatus Status { get; init; } = AppUpdateStatus.Disabled;

    public string CurrentVersion { get; init; } = "0.0.0";

    public string? ManifestUrl { get; init; }

    public AppUpdateRelease? AvailableRelease { get; init; }

    public string? DownloadedPackagePath { get; init; }

    public DateTimeOffset? LastCheckedAtUtc { get; init; }

    public string? LastError { get; init; }

    public bool IsEnabled => !string.IsNullOrWhiteSpace(ManifestUrl);

    public bool IsUpdateAvailable => AvailableRelease is not null;

    public bool CanInstall => Status == AppUpdateStatus.ReadyToInstall
                              && !string.IsNullOrWhiteSpace(DownloadedPackagePath);

    public static AppUpdateState Disabled(string currentVersion, string? manifestUrl = null, string? reason = null) => new()
    {
        Status = AppUpdateStatus.Disabled,
        CurrentVersion = currentVersion,
        ManifestUrl = manifestUrl,
        LastError = reason
    };
}
