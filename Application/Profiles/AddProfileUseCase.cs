using VpnClient.Core.Interfaces;
using VpnClient.Core.Models;

namespace VpnClient.Application.Profiles;

public sealed class AddProfileUseCase
{
    private readonly IProfileRepository _repository;

    public AddProfileUseCase(IProfileRepository repository)
    {
        _repository = repository;
    }

    public async Task<ProfileCollectionSnapshot> ExecuteAsync(ImportedServerProfile profile, CancellationToken cancellationToken = default)
    {
        var state = await _repository.AddAsync(profile, cancellationToken);
        return ListProfilesUseCase.ToSnapshot(state);
    }
}
