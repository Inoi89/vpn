using VpnClient.Core.Interfaces;

namespace VpnClient.Application.Profiles;

public sealed class DeleteProfileUseCase
{
    private readonly IProfileRepository _repository;

    public DeleteProfileUseCase(IProfileRepository repository)
    {
        _repository = repository;
    }

    public async Task<ProfileCollectionSnapshot> ExecuteAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        var state = await _repository.DeleteAsync(profileId, cancellationToken);
        return ListProfilesUseCase.ToSnapshot(state);
    }
}
