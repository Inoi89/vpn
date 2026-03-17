using Microsoft.Extensions.Options;
using VpnNodeAgent.Abstractions;
using VpnNodeAgent.Configuration;

namespace VpnNodeAgent.Services;

public sealed class ConfigFileReader(
    IOptions<AgentOptions> options,
    ProcessCommandExecutor commandExecutor) : IConfigFileReader
{
    public async Task<IReadOnlyList<string>> ReadAllLinesAsync(string filePath, CancellationToken cancellationToken)
    {
        if (string.Equals(options.Value.OperationMode, "Docker", StringComparison.OrdinalIgnoreCase))
        {
            var containerName = GetRequiredContainerName();
            var output = await commandExecutor.ExecuteAsync(
                options.Value.DockerExecutablePath,
                ["exec", containerName, "cat", filePath],
                cancellationToken);

            return output.Split(['\r', '\n'], StringSplitOptions.None);
        }

        return await File.ReadAllLinesAsync(filePath, cancellationToken);
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
