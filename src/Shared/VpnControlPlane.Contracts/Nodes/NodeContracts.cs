namespace VpnControlPlane.Contracts.Nodes;

public sealed record RegisterNodeRequest(
    string AgentIdentifier,
    string Name,
    string Cluster,
    string AgentBaseAddress,
    string? CertificateThumbprint,
    string? Description);

public sealed record NodeRegistrationResponse(
    Guid NodeId,
    string AgentIdentifier,
    string Name,
    string Cluster,
    string AgentBaseAddress,
    string Status);

public sealed record NodeSnapshotResponse(
    string AgentIdentifier,
    string Hostname,
    string AgentVersion,
    DateTimeOffset CollectedAtUtc,
    IReadOnlyList<AgentSessionSnapshot> Sessions,
    IReadOnlyList<PeerConfigSnapshot> PeerConfigs);

public sealed record AgentSessionSnapshot(
    string PublicKey,
    string? UserExternalId,
    string? UserEmail,
    string? UserDisplayName,
    string Protocol,
    string? InterfaceName,
    string? Endpoint,
    DateTimeOffset? LatestHandshakeAtUtc,
    long RxBytes,
    long TxBytes,
    bool IsActive);

public sealed record PeerConfigSnapshot(
    string PublicKey,
    string? UserExternalId,
    string? UserEmail,
    string? UserDisplayName,
    string Protocol,
    IReadOnlyList<string> AllowedIps,
    string? MetadataJson,
    int Revision);
