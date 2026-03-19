using VpnClient.Core.Models.Auth;

namespace VpnClient.Core.Interfaces;

public interface IProductPlatformAuthService
{
    Task<ProductPlatformSession?> GetCurrentSessionAsync(CancellationToken cancellationToken = default);

    Task<ProductPlatformSession> LoginAsync(string email, string password, CancellationToken cancellationToken = default);

    Task LogoutAsync(CancellationToken cancellationToken = default);
}
