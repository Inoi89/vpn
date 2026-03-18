using VpnClient.Core.Models;

namespace VpnClient.Application.Profiles;

public sealed record ProfileCollectionSnapshot(
    Guid? ActiveProfileId,
    List<ImportedServerProfile> Profiles)
{
    public ImportedServerProfile? ActiveProfile =>
        Profiles.FirstOrDefault(profile => profile.Id == ActiveProfileId);
}
