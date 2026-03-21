using Xunit;
using VpnClient.Infrastructure.Logging;

namespace VpnClient.Tests;

public sealed class FileLogWriterTests
{
    [Fact]
    public void LogFilePath_EndsWithClientLog()
    {
        var path = FileLogWriter.GetLogFilePath();

        Assert.EndsWith(Path.Combine("logs", "client.log"), path);
    }

    [Fact]
    public void LogDirectoryPath_UsesWritableUserRootOnMac()
    {
        var path = FileLogWriter.GetLogDirectoryPath();

        if (OperatingSystem.IsMacOS())
        {
            var expectedRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "YourVpnClient");

            Assert.StartsWith(expectedRoot, path, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            Assert.StartsWith(AppContext.BaseDirectory, path, StringComparison.OrdinalIgnoreCase);
        }
    }
}
