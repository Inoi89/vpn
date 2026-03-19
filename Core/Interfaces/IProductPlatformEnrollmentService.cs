using VpnClient.Core.Models;

namespace VpnClient.Core.Interfaces;

public interface IProductPlatformEnrollmentService
{
    Task<ImportedServerProfile> EnsureManagedProfileAsync(CancellationToken cancellationToken = default);
}
