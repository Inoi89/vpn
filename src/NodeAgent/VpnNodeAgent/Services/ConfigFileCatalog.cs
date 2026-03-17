using Microsoft.Extensions.Options;
using VpnNodeAgent.Abstractions;
using VpnNodeAgent.Configuration;

namespace VpnNodeAgent.Services;

public sealed class ConfigFileCatalog(
    IOptions<AgentOptions> options,
    ProcessCommandExecutor commandExecutor) : IConfigFileCatalog
{
    public async Task<IReadOnlyList<string>> ListConfigFilesAsync(CancellationToken cancellationToken)
    {
        if (string.Equals(options.Value.OperationMode, "Docker", StringComparison.OrdinalIgnoreCase))
        {
            return await ListFromDockerAsync(cancellationToken);
        }

        var root = options.Value.ConfigDirectory;
        if (!Directory.Exists(root))
        {
            return [];
        }

        var results = new List<string>();
        foreach (var pattern in options.Value.ConfigSearchPatterns.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            results.AddRange(Directory.EnumerateFiles(root, pattern, SearchOption.TopDirectoryOnly));
        }

        return results.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private async Task<IReadOnlyList<string>> ListFromDockerAsync(CancellationToken cancellationToken)
    {
        var containerName = GetRequiredContainerName();
        var root = options.Value.ConfigDirectory;
        var results = new List<string>();

        foreach (var pattern in options.Value.ConfigSearchPatterns.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            var output = await commandExecutor.ExecuteAsync(
                options.Value.DockerExecutablePath,
                ["exec", containerName, "find", root, "-maxdepth", "1", "-type", "f", "-name", pattern, "-print"],
                cancellationToken);

            results.AddRange(
                output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        return results.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
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
