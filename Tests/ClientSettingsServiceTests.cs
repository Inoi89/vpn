using VpnClient.Core.Models;
using VpnClient.Infrastructure.Persistence;
using Xunit;

namespace VpnClient.Tests;

public sealed class ClientSettingsServiceTests
{
    [Fact]
    public async Task LoadAsync_ReturnsDefaults_WhenFileIsMissing()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var filePath = Path.Combine(tempDirectory, "client-settings.json");
            var service = new JsonClientSettingsService(filePath);

            var settings = await service.LoadAsync();

            Assert.True(settings.AutoConnectEnabled);
            Assert.False(settings.KillSwitchEnabled);
            Assert.True(settings.NotificationsEnabled);
            Assert.True(settings.LaunchToTrayEnabled);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_PersistsSettingsRoundTrip()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var filePath = Path.Combine(tempDirectory, "client-settings.json");
            var service = new JsonClientSettingsService(filePath);
            var expected = new ClientSettings(
                AutoConnectEnabled: false,
                KillSwitchEnabled: true,
                NotificationsEnabled: false,
                LaunchToTrayEnabled: false);

            await service.SaveAsync(expected);
            var actual = await service.LoadAsync();

            Assert.Equal(expected, actual);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void DefaultStoragePath_IsAppDataBased()
    {
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "YourVpnClient",
            "client-settings.json");

        Assert.Equal(expected, JsonClientSettingsService.GetDefaultStoragePath());
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "vpnclient-settings-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
