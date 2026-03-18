namespace VpnClient.Infrastructure.Updates;

public sealed class AppUpdateOptions
{
    public string? ManifestUrl { get; init; }

    public string Channel { get; init; } = "stable";

    public string? DownloadRootDirectory { get; init; }

    public static string GetDefaultDownloadRootDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "YourVpnClient",
            "Updates");
    }
}
