namespace VpnClient.Infrastructure.Runtime;

public interface IWindowsRuntimeAssetLocator
{
    string ApplicationBaseDirectory { get; }

    string RuntimeRootDirectory { get; }

    string WireGuardRuntimeDirectory { get; }

    string AmneziaWgExecutablePath { get; }

    string AwgExecutablePath { get; }

    string WgExecutablePath { get; }

    string WintunDllPath { get; }

    string? WireGuardServiceExecutablePath { get; }

    string? TunnelDllPath { get; }

    string? WireGuardDllPath { get; }

    bool HasBundledAmneziaWgExecutable { get; }

    bool HasBundledAwgExecutable { get; }

    bool HasBundledWgExecutable { get; }

    bool HasBundledWintun { get; }

    IReadOnlyList<string> GetWarnings();
}

public sealed class WindowsRuntimeAssetLocator : IWindowsRuntimeAssetLocator
{
    private readonly string _applicationBaseDirectory;
    private readonly string _runtimeRootDirectory;
    private readonly string _wireGuardRuntimeDirectory;
    private readonly string _amneziaWgExecutablePath;
    private readonly string _awgExecutablePath;
    private readonly string _wgExecutablePath;
    private readonly string _wintunDllPath;
    private readonly string? _wireGuardServiceExecutablePath;
    private readonly string? _tunnelDllPath;
    private readonly string? _wireGuardDllPath;
    private readonly string[] _warnings;

    public WindowsRuntimeAssetLocator()
        : this(AppContext.BaseDirectory)
    {
    }

    public WindowsRuntimeAssetLocator(string applicationBaseDirectory)
    {
        if (string.IsNullOrWhiteSpace(applicationBaseDirectory))
        {
            throw new ArgumentException("Application base directory is required.", nameof(applicationBaseDirectory));
        }

        _applicationBaseDirectory = Path.GetFullPath(applicationBaseDirectory);
        _runtimeRootDirectory = Path.Combine(_applicationBaseDirectory, "runtime");
        _wireGuardRuntimeDirectory = Path.Combine(_runtimeRootDirectory, "wireguard");

        var bundledAmneziaWgExecutable = Path.Combine(_wireGuardRuntimeDirectory, "amneziawg.exe");
        var bundledAwgExecutable = Path.Combine(_wireGuardRuntimeDirectory, "awg.exe");
        var bundledWintunDll = Path.Combine(_wireGuardRuntimeDirectory, "wintun.dll");
        var bundledWireGuardService = Path.Combine(_wireGuardRuntimeDirectory, "wireguard-service.exe");
        var bundledTunnelDll = Path.Combine(_wireGuardRuntimeDirectory, "tunnel.dll");
        var bundledWireGuardDll = Path.Combine(_wireGuardRuntimeDirectory, "wireguard.dll");

        HasBundledAmneziaWgExecutable = File.Exists(bundledAmneziaWgExecutable);
        HasBundledAwgExecutable = File.Exists(bundledAwgExecutable);
        HasBundledWgExecutable = HasBundledAwgExecutable;
        HasBundledWintun = File.Exists(bundledWintunDll);

        _amneziaWgExecutablePath = HasBundledAmneziaWgExecutable ? bundledAmneziaWgExecutable : "amneziawg.exe";
        _awgExecutablePath = HasBundledAwgExecutable ? bundledAwgExecutable : "awg.exe";
        _wgExecutablePath = _awgExecutablePath;
        _wintunDllPath = HasBundledWintun ? bundledWintunDll : "wintun.dll";
        _wireGuardServiceExecutablePath = File.Exists(bundledWireGuardService) ? bundledWireGuardService : null;
        _tunnelDllPath = File.Exists(bundledTunnelDll) ? bundledTunnelDll : null;
        _wireGuardDllPath = File.Exists(bundledWireGuardDll) ? bundledWireGuardDll : null;

        _warnings = BuildWarnings().ToArray();
    }

    public string ApplicationBaseDirectory => _applicationBaseDirectory;

    public string RuntimeRootDirectory => _runtimeRootDirectory;

    public string WireGuardRuntimeDirectory => _wireGuardRuntimeDirectory;

    public string AmneziaWgExecutablePath => _amneziaWgExecutablePath;

    public string AwgExecutablePath => _awgExecutablePath;

    public string WgExecutablePath => _wgExecutablePath;

    public string WintunDllPath => _wintunDllPath;

    public string? WireGuardServiceExecutablePath => _wireGuardServiceExecutablePath;

    public string? TunnelDllPath => _tunnelDllPath;

    public string? WireGuardDllPath => _wireGuardDllPath;

    public bool HasBundledAmneziaWgExecutable { get; }

    public bool HasBundledAwgExecutable { get; }

    public bool HasBundledWgExecutable { get; }

    public bool HasBundledWintun { get; }

    public IReadOnlyList<string> GetWarnings() => _warnings;

    private IEnumerable<string> BuildWarnings()
    {
        if (!OperatingSystem.IsWindows())
        {
            yield break;
        }

        if (!HasBundledAmneziaWgExecutable)
        {
            yield return "Bundled AmneziaWG executable is missing from runtime/wireguard. The autonomous Windows runtime will be unavailable.";
        }

        if (!HasBundledAwgExecutable)
        {
            yield return "Bundled AWG CLI is missing from runtime/wireguard. Status probing and service-side WireGuard control will be unavailable.";
        }

        if (!HasBundledWintun)
        {
            yield return "Bundled Wintun runtime is missing from runtime/wireguard. The client will rely on wintun.dll being available on the machine.";
        }
    }
}
