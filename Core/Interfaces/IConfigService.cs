using VpnClient.Core.Models;

namespace VpnClient.Core.Interfaces;

public interface IConfigService
{
    ImportedProfile? CurrentProfile { get; }

    Task<ImportedProfile> ImportConfigAsync(string path);

    Task<string> LoadConfigAsync();
}
