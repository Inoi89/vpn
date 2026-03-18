using VpnClient.Core.Models;

namespace VpnClient.Core.Interfaces;

public interface IImportService
{
    Task<ImportedTunnelConfig> ImportAsync(string path, CancellationToken cancellationToken = default);
}
