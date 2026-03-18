using System.Text;
using Microsoft.Extensions.Logging;

namespace VpnClient.Infrastructure.Logging;

internal static class FileLogWriter
{
    private static readonly object Gate = new();

    public static string GetLogDirectoryPath()
    {
        return Path.Combine(AppContext.BaseDirectory, "logs");
    }

    public static string GetLogFilePath()
    {
        return Path.Combine(GetLogDirectoryPath(), "client.log");
    }

    public static void Write(string source, LogLevel level, string message, Exception? exception = null)
    {
        var builder = new StringBuilder()
            .Append('[')
            .Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
            .Append("] [")
            .Append(level)
            .Append("] [")
            .Append(source)
            .Append("] ")
            .Append(message);

        if (exception is not null)
        {
            builder.AppendLine()
                .Append(exception);
        }

        Append(builder.ToString());
    }

    private static void Append(string content)
    {
        lock (Gate)
        {
            var directoryPath = GetLogDirectoryPath();
            Directory.CreateDirectory(directoryPath);
            File.AppendAllText(GetLogFilePath(), content + Environment.NewLine, Encoding.UTF8);
        }
    }
}
