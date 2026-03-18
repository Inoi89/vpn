using VpnClient.Core.Interfaces;
using VpnClient.Core.Models;

namespace VpnClient.Application.Profiles;

public sealed class ImportProfileUseCase
{
    private readonly IImportService _importService;
    private readonly IProfileRepository _repository;

    public ImportProfileUseCase(IImportService importService, IProfileRepository repository)
    {
        _importService = importService;
        _repository = repository;
    }

    public async Task<ImportProfileResult> ExecuteAsync(string path, CancellationToken cancellationToken = default)
    {
        var imported = await _importService.ImportAsync(path, cancellationToken);
        var profile = new ImportedServerProfile(
            Guid.NewGuid(),
            imported.DisplayName,
            imported,
            imported.ImportedAtUtc,
            imported.ImportedAtUtc);

        var state = await _repository.AddAsync(profile, cancellationToken);
        return new ImportProfileResult(profile, ListProfilesUseCase.ToSnapshot(state));
    }
}

public sealed record ImportProfileResult(
    ImportedServerProfile Profile,
    ProfileCollectionSnapshot Snapshot);
