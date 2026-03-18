using System.Runtime.InteropServices;
using VpnClient.Core.Interfaces;
using VpnClient.Infrastructure.Runtime;

namespace VpnClient.Infrastructure.Services;

public sealed class WintunService : IWintunService, IDisposable
{
    private readonly IWindowsRuntimeAssetLocator _assetLocator;
    private readonly object _syncRoot = new();
    private IntPtr _libraryHandle;
    private WintunCreateAdapterDelegate? _createAdapter;
    private WintunOpenAdapterDelegate? _openAdapter;
    private WintunCloseAdapterDelegate? _closeAdapter;
    private WintunDeleteAdapterDelegate? _deleteAdapter;

    public WintunService(IWindowsRuntimeAssetLocator assetLocator)
    {
        _assetLocator = assetLocator;
    }

    public Task CreateAdapterAsync(string name)
    {
        var api = GetApi();
        var handle = api.OpenAdapter(name);
        if (handle == IntPtr.Zero)
        {
            handle = api.CreateAdapter(name, "VPN", IntPtr.Zero);
        }

        if (handle != IntPtr.Zero)
        {
            api.CloseAdapter(handle);
        }

        return Task.CompletedTask;
    }

    public Task DeleteAdapterAsync(string name)
    {
        var api = GetApi();
        var handle = api.OpenAdapter(name);
        if (handle != IntPtr.Zero)
        {
            api.DeleteAdapter(handle);
            api.CloseAdapter(handle);
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_libraryHandle == IntPtr.Zero)
        {
            return;
        }

        lock (_syncRoot)
        {
            if (_libraryHandle == IntPtr.Zero)
            {
                return;
            }

            NativeLibrary.Free(_libraryHandle);
            _libraryHandle = IntPtr.Zero;
            _createAdapter = null;
            _openAdapter = null;
            _closeAdapter = null;
            _deleteAdapter = null;
        }
    }

    private WintunApi GetApi()
    {
        EnsureLoaded();

        return new WintunApi(
            _createAdapter ?? throw new InvalidOperationException("WintunCreateAdapter entry point is unavailable."),
            _openAdapter ?? throw new InvalidOperationException("WintunOpenAdapter entry point is unavailable."),
            _closeAdapter ?? throw new InvalidOperationException("WintunCloseAdapter entry point is unavailable."),
            _deleteAdapter ?? throw new InvalidOperationException("WintunDeleteAdapter entry point is unavailable."));
    }

    private void EnsureLoaded()
    {
        if (_libraryHandle != IntPtr.Zero)
        {
            return;
        }

        lock (_syncRoot)
        {
            if (_libraryHandle != IntPtr.Zero)
            {
                return;
            }

            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException("Wintun is available only on Windows.");
            }

            if (!NativeLibrary.TryLoad(_assetLocator.WintunDllPath, out _libraryHandle))
            {
                throw new DllNotFoundException(
                    $"Unable to load Wintun runtime from '{_assetLocator.WintunDllPath}'. Bundle wintun.dll into runtime\\wireguard or install a compatible system runtime first.");
            }

            _createAdapter = LoadFunction<WintunCreateAdapterDelegate>("WintunCreateAdapter");
            _openAdapter = LoadFunction<WintunOpenAdapterDelegate>("WintunOpenAdapter");
            _closeAdapter = LoadFunction<WintunCloseAdapterDelegate>("WintunCloseAdapter");
            _deleteAdapter = LoadFunction<WintunDeleteAdapterDelegate>("WintunDeleteAdapter");
        }
    }

    private T LoadFunction<T>(string exportName) where T : Delegate
    {
        if (_libraryHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Wintun library has not been loaded.");
        }

        var symbol = NativeLibrary.GetExport(_libraryHandle, exportName);
        return Marshal.GetDelegateForFunctionPointer<T>(symbol);
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi, CharSet = CharSet.Unicode)]
    private delegate IntPtr WintunCreateAdapterDelegate(string name, string type, IntPtr requestedGuid);

    [UnmanagedFunctionPointer(CallingConvention.Winapi, CharSet = CharSet.Unicode)]
    private delegate IntPtr WintunOpenAdapterDelegate(string name);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate void WintunCloseAdapterDelegate(IntPtr handle);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate void WintunDeleteAdapterDelegate(IntPtr handle);

    private sealed record WintunApi(
        WintunCreateAdapterDelegate CreateAdapter,
        WintunOpenAdapterDelegate OpenAdapter,
        WintunCloseAdapterDelegate CloseAdapter,
        WintunDeleteAdapterDelegate DeleteAdapter);
}
