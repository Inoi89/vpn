using Microsoft.Extensions.Options;
using VpnNodeAgent.Abstractions;
using VpnNodeAgent.Configuration;

namespace VpnNodeAgent.Services;

public sealed class ConfigFileCatalog(IOptions<AgentOptions> options) : IConfigFileCatalog
{
    public Task<IReadOnlyList<string>> ListConfigFilesAsync(CancellationToken cancellationToken)
    {
        var root = options.Value.ConfigDirectory;
        if (!Directory.Exists(root))
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        var results = new List<string>();
        foreach (var pattern in options.Value.ConfigSearchPatterns.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            results.AddRange(Directory.EnumerateFiles(root, pattern, SearchOption.TopDirectoryOnly));
        }

        return Task.FromResult<IReadOnlyList<string>>(results.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }
}
