using VpnClient.Core.Models;

namespace VpnClient.Core.Interfaces;

public interface IProfileRepository
{
    Task<ProfileCollectionState> LoadAsync(CancellationToken cancellationToken = default);

    Task<ProfileCollectionState> AddAsync(ImportedServerProfile profile, CancellationToken cancellationToken = default);

    Task<ProfileCollectionState> DeleteAsync(Guid profileId, CancellationToken cancellationToken = default);

    Task<ProfileCollectionState> RenameAsync(Guid profileId, string displayName, CancellationToken cancellationToken = default);

    Task<ProfileCollectionState> SetActiveAsync(Guid profileId, CancellationToken cancellationToken = default);
}
