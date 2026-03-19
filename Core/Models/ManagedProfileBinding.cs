namespace VpnClient.Core.Models;

public sealed record ManagedProfileBinding(
    Guid AccountId,
    string AccountEmail,
    Guid DeviceId,
    Guid AccessGrantId,
    Guid NodeId,
    Guid? ControlPlaneAccessId,
    string ConfigFormat);
