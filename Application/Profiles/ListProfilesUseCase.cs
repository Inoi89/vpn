using VpnClient.Core.Interfaces;
using VpnClient.Core.Models;

namespace VpnClient.Application.Profiles;

public sealed class ListProfilesUseCase
{
    private readonly IProfileRepository _repository;

    public ListProfilesUseCase(IProfileRepository repository)
    {
        _repository = repository;
    }

    public async Task<ProfileCollectionSnapshot> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var state = await _repository.LoadAsync(cancellationToken);
        return ToSnapshot(state);
    }

    internal static ProfileCollectionSnapshot ToSnapshot(ProfileCollectionState state)
    {
        var ordered = state.Profiles
            .OrderByDescending(profile => profile.Id == state.ActiveProfileId)
            .ThenByDescending(profile => profile.ImportedAtUtc)
            .ToList();

        return new ProfileCollectionSnapshot(state.ActiveProfileId, ordered);
    }
}
