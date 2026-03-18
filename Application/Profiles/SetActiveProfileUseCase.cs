using VpnClient.Core.Interfaces;

namespace VpnClient.Application.Profiles;

public sealed class SetActiveProfileUseCase
{
    private readonly IProfileRepository _repository;

    public SetActiveProfileUseCase(IProfileRepository repository)
    {
        _repository = repository;
    }

    public async Task<ProfileCollectionSnapshot> ExecuteAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        var state = await _repository.SetActiveAsync(profileId, cancellationToken);
        return ListProfilesUseCase.ToSnapshot(state);
    }
}
