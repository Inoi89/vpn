using System.IO;
using VpnClient.Core.Interfaces;

namespace VpnClient.Infrastructure.Services;

public class ConfigService : IConfigService
{
    private readonly string _path;

    public ConfigService(string path)
    {
        _path = path;
    }

    public Task<string> LoadConfigAsync()
    {
        return File.ReadAllTextAsync(_path);
    }
}
