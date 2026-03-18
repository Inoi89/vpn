namespace VpnClient.Core.Models;

public sealed record ProfileCollectionState(
    Guid? ActiveProfileId,
    List<ImportedServerProfile> Profiles);
