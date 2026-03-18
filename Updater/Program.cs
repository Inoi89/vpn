using System.Diagnostics;
using System.Globalization;

if (!OperatingSystem.IsWindows())
{
    return 1;
}

var options = ParseArguments(args);
if (options is null)
{
    return 2;
}

try
{
    await WaitForProcessExitAsync(options.WaitPid, TimeSpan.FromMinutes(2));

    var msiArguments = $"/i \"{options.PackagePath}\" /passive /norestart";
    var installProcess = Process.Start(new ProcessStartInfo
    {
        FileName = "msiexec.exe",
        Arguments = msiArguments,
        UseShellExecute = true,
        Verb = "runas"
    });

    if (installProcess is null)
    {
        return 3;
    }

    await installProcess.WaitForExitAsync();
    if (installProcess.ExitCode is not 0 and not 1641 and not 3010)
    {
        return installProcess.ExitCode;
    }

    if (!string.IsNullOrWhiteSpace(options.RestartPath) && File.Exists(options.RestartPath))
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = options.RestartPath,
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(options.RestartPath) ?? AppContext.BaseDirectory
        });
    }

    return 0;
}
catch
{
    return 4;
}

static UpdateLaunchOptions? ParseArguments(string[] args)
{
    string? packagePath = null;
    string? restartPath = null;
    int? waitPid = null;

    for (var index = 0; index < args.Length; index++)
    {
        switch (args[index])
        {
            case "--package" when index + 1 < args.Length:
                packagePath = args[++index];
                break;
            case "--restart" when index + 1 < args.Length:
                restartPath = args[++index];
                break;
            case "--wait-pid" when index + 1 < args.Length:
                if (int.TryParse(args[++index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPid))
                {
                    waitPid = parsedPid;
                }
                break;
        }
    }

    if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
    {
        return null;
    }

    return new UpdateLaunchOptions(packagePath, restartPath, waitPid);
}

static async Task WaitForProcessExitAsync(int? processId, TimeSpan timeout)
{
    if (processId is null or <= 0)
    {
        return;
    }

    try
    {
        using var process = Process.GetProcessById(processId.Value);
        using var cts = new CancellationTokenSource(timeout);
        await process.WaitForExitAsync(cts.Token);
    }
    catch (ArgumentException)
    {
    }
    catch (OperationCanceledException)
    {
    }
}

sealed record UpdateLaunchOptions(string PackagePath, string? RestartPath, int? WaitPid);
