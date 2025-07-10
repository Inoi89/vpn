using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace WintunWrapper
{
    public class AdapterManager
    {
        [DllImport("wintun.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private static extern IntPtr WintunCreateAdapter(string name, string tunnelType, IntPtr reserved);

        [DllImport("wintun.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private static extern IntPtr WintunOpenAdapter(string name);

        [DllImport("wintun.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern void WintunCloseAdapter(IntPtr handle);

        [DllImport("wintun.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern bool WintunDeleteDriver();

        public async Task<object> CreateAdapter(dynamic input)
        {
            IntPtr handle = WintunOpenAdapter("SimVPN");
            if (handle == IntPtr.Zero)
            {
                handle = WintunCreateAdapter("SimVPN", "VPN", IntPtr.Zero);
            }
            return null;
        }

        public async Task<object> DeleteAdapter(dynamic input)
        {
            WintunDeleteDriver();
            return null;
        }
    }
}
