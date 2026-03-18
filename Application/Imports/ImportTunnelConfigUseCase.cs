using VpnClient.Core.Interfaces;
using VpnClient.Core.Models;

namespace VpnClient.Application.Imports;

public sealed class ImportTunnelConfigUseCase
{
    private readonly IImportService _importService;

    public ImportTunnelConfigUseCase(IImportService importService)
    {
        _importService = importService;
    }

    public Task<ImportedTunnelConfig> ExecuteAsync(string path, CancellationToken cancellationToken = default)
    {
        return _importService.ImportAsync(path, cancellationToken);
    }
}
