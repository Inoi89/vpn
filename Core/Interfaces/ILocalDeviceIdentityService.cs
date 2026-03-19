using VpnClient.Core.Models.Auth;

namespace VpnClient.Core.Interfaces;

public interface ILocalDeviceIdentityService
{
    Task<LocalDeviceIdentity> GetOrCreateAsync(CancellationToken cancellationToken = default);
}
