using VpnClient.Core.Interfaces;

namespace VpnClient.Application.Profiles;

public sealed class RenameProfileUseCase
{
    private readonly IProfileRepository _repository;

    public RenameProfileUseCase(IProfileRepository repository)
    {
        _repository = repository;
    }

    public async Task<ProfileCollectionSnapshot> ExecuteAsync(Guid profileId, string displayName, CancellationToken cancellationToken = default)
    {
        var state = await _repository.RenameAsync(profileId, displayName, cancellationToken);
        return ListProfilesUseCase.ToSnapshot(state);
    }
}
