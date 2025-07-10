namespace VpnClient.Core.Interfaces;

public interface IConfigService
{
    Task<string> LoadConfigAsync();
}
