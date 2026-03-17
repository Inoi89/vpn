using Microsoft.Extensions.Options;
using VpnNodeAgent.Abstractions;
using VpnNodeAgent.Configuration;

namespace VpnNodeAgent.Services;

public sealed class ConfigFileWriter(
    IOptions<AgentOptions> options,
    ProcessCommandExecutor commandExecutor) : IConfigFileWriter
{
    public async Task WriteAllTextAsync(string filePath, string contents, CancellationToken cancellationToken)
    {
        if (string.Equals(options.Value.OperationMode, "Docker", StringComparison.OrdinalIgnoreCase))
        {
            var tempPath = Path.GetTempFileName();

            try
            {
                await File.WriteAllTextAsync(tempPath, contents, cancellationToken);
                await commandExecutor.ExecuteAsync(
                    options.Value.DockerExecutablePath,
                    ["cp", tempPath, $"{GetRequiredContainerName()}:{filePath}"],
                    cancellationToken);
            }
            finally
            {
                File.Delete(tempPath);
            }

            return;
        }

        await File.WriteAllTextAsync(filePath, contents, cancellationToken);
    }

    private string GetRequiredContainerName()
    {
        if (string.IsNullOrWhiteSpace(options.Value.DockerContainerName))
        {
            throw new InvalidOperationException("Agent:DockerContainerName is required when Agent:OperationMode is Docker.");
        }

        return options.Value.DockerContainerName;
    }
}
