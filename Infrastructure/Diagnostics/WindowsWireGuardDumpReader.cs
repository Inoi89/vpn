using System.Diagnostics;
using VpnClient.Core.Interfaces;

namespace VpnClient.Infrastructure.Diagnostics;

public sealed class WindowsWireGuardDumpReader : IWireGuardDumpReader
{
    private readonly string _wgExecutablePath;

    public WindowsWireGuardDumpReader()
        : this("wg.exe")
    {
    }

    public WindowsWireGuardDumpReader(string wgExecutablePath)
    {
        _wgExecutablePath = string.IsNullOrWhiteSpace(wgExecutablePath) ? "wg.exe" : wgExecutablePath;
    }

    public async Task<string?> ReadDumpAsync(string interfaceName, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(interfaceName))
        {
            return null;
        }

        var psi = new ProcessStartInfo(_wgExecutablePath, $"show \"{interfaceName}\" dump")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var process = Process.Start(psi);
            if (process is null)
            {
                return null;
            }

            var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            _ = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output)
                ? output
                : null;
        }
        catch
        {
            return null;
        }
    }
}
