using VpnClient.Infrastructure.Runtime;
using Xunit;

namespace VpnClient.Tests.Runtime;

public sealed class WindowsRuntimeAssetLocatorTests : IDisposable
{
    private readonly string _rootDirectory;

    public WindowsRuntimeAssetLocatorTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), "vpnclient-runtime-locator", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDirectory);
    }

    [Fact]
    public void Locator_PrefersBundledRuntimeAssets_WhenTheyExist()
    {
        var runtimeDirectory = Path.Combine(_rootDirectory, "runtime", "wireguard");
        Directory.CreateDirectory(runtimeDirectory);

        File.WriteAllText(Path.Combine(runtimeDirectory, "amneziawg.exe"), string.Empty);
        File.WriteAllText(Path.Combine(runtimeDirectory, "awg.exe"), string.Empty);
        File.WriteAllText(Path.Combine(runtimeDirectory, "wintun.dll"), string.Empty);

        var locator = new WindowsRuntimeAssetLocator(_rootDirectory);

        Assert.True(locator.HasBundledAmneziaWgExecutable);
        Assert.True(locator.HasBundledAwgExecutable);
        Assert.True(locator.HasBundledWgExecutable);
        Assert.True(locator.HasBundledWintun);
        Assert.Equal(Path.Combine(runtimeDirectory, "amneziawg.exe"), locator.AmneziaWgExecutablePath);
        Assert.Equal(Path.Combine(runtimeDirectory, "awg.exe"), locator.AwgExecutablePath);
        Assert.Equal(Path.Combine(runtimeDirectory, "awg.exe"), locator.WgExecutablePath);
        Assert.Equal(Path.Combine(runtimeDirectory, "wintun.dll"), locator.WintunDllPath);
    }

    [Fact]
    public void Locator_FallsBackToSystemLookup_WhenBundledAssetsAreMissing()
    {
        var locator = new WindowsRuntimeAssetLocator(_rootDirectory);

        Assert.False(locator.HasBundledAmneziaWgExecutable);
        Assert.False(locator.HasBundledAwgExecutable);
        Assert.False(locator.HasBundledWgExecutable);
        Assert.False(locator.HasBundledWintun);
        Assert.Equal("amneziawg.exe", locator.AmneziaWgExecutablePath);
        Assert.Equal("awg.exe", locator.AwgExecutablePath);
        Assert.Equal("awg.exe", locator.WgExecutablePath);
        Assert.Equal("wintun.dll", locator.WintunDllPath);
        Assert.Contains(locator.GetWarnings(), warning => warning.Contains("Bundled AmneziaWG executable", StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }
}
