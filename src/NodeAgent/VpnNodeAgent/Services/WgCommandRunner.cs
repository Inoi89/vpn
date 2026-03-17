using System.Diagnostics;
using Microsoft.Extensions.Options;
using VpnNodeAgent.Abstractions;
using VpnNodeAgent.Configuration;

namespace VpnNodeAgent.Services;

public sealed class WgCommandRunner(IOptions<AgentOptions> options) : IWireGuardCommandRunner
{
    public async Task<string> ExecuteShowAllDumpAsync(CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = options.Value.WgExecutablePath,
            Arguments = "show all dump",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"wg show all dump failed with exit code {process.ExitCode}: {error}");
        }

        return output;
    }
}
