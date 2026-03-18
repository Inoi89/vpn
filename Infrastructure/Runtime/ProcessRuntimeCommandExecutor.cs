using System.Diagnostics;

namespace VpnClient.Infrastructure.Runtime;

public sealed class ProcessRuntimeCommandExecutor : IRuntimeCommandExecutor
{
    public async Task<RuntimeCommandResult> ExecuteAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return new RuntimeCommandResult(-1, string.Empty, $"Failed to start process '{fileName}'.");
        }

        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(cancellationToken);

        return new RuntimeCommandResult(
            process.ExitCode,
            await standardOutputTask,
            await standardErrorTask);
    }
}
