namespace VpnProductPlatform.Application.Abstractions;

public interface IControlPlaneProvisioningClient
{
    Task<IReadOnlyList<ControlPlaneNodeEnvelope>> ListNodesAsync(CancellationToken cancellationToken);

    Task<ControlPlaneIssuedAccessEnvelope> IssueAccessAsync(ControlPlaneIssueAccessRequest request, CancellationToken cancellationToken);

    Task<ControlPlaneAccessConfigEnvelope> GetAccessConfigAsync(Guid nodeId, Guid accessId, string configFormat, CancellationToken cancellationToken);
}

public sealed record ControlPlaneNodeEnvelope(
    Guid NodeId,
    string Name,
    string Status,
    int ActiveSessions,
    int EnabledPeerCount);

public sealed record ControlPlaneIssueAccessRequest(
    Guid NodeId,
    string DisplayName,
    string? Email,
    string ConfigFormat,
    ControlPlaneProductMetadata ProductMetadata);

public sealed record ControlPlaneIssuedAccessEnvelope(
    Guid NodeId,
    Guid? AccessId,
    Guid UserId,
    string ExternalId,
    string DisplayName,
    string? Email,
    string PublicKey,
    string AllowedIps,
    string ClientConfigFileName,
    string ClientConfig);

public sealed record ControlPlaneAccessConfigEnvelope(
    Guid NodeId,
    Guid AccessId,
    string Format,
    string FileName,
    string Config);

public sealed record ControlPlaneProductMetadata(
    Guid AccountId,
    string AccountEmail,
    string AccountDisplayName,
    Guid DeviceId,
    string DeviceName,
    string DevicePlatform,
    string DeviceFingerprint,
    string? ClientVersion);
