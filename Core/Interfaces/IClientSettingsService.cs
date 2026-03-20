using VpnClient.Core.Models;

namespace VpnClient.Core.Interfaces;

public interface IClientSettingsService
{
    Task<ClientSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(ClientSettings settings, CancellationToken cancellationToken = default);
}
