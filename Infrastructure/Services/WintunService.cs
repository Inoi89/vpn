using System.Runtime.InteropServices;
using VpnClient.Core.Interfaces;

namespace VpnClient.Infrastructure.Services;

public class WintunService : IWintunService
{
    public Task CreateAdapterAsync(string name)
    {
        var handle = WintunOpenAdapter(name);
        if (handle == IntPtr.Zero)
            handle = WintunCreateAdapter(name, "VPN", IntPtr.Zero);
        if (handle != IntPtr.Zero)
            WintunCloseAdapter(handle);
        return Task.CompletedTask;
    }

    public Task DeleteAdapterAsync(string name)
    {
        var handle = WintunOpenAdapter(name);
        if (handle != IntPtr.Zero)
        {
            WintunDeleteAdapter(handle);
            WintunCloseAdapter(handle);
        }
        return Task.CompletedTask;
    }

    [DllImport("wintun.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr WintunCreateAdapter(string name, string type, IntPtr requestedGuid);

    [DllImport("wintun.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr WintunOpenAdapter(string name);

    [DllImport("wintun.dll")] 
    private static extern void WintunCloseAdapter(IntPtr handle);

    [DllImport("wintun.dll")]
    private static extern void WintunDeleteAdapter(IntPtr handle);
}
