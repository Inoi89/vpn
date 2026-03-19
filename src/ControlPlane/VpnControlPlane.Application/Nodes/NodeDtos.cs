namespace VpnControlPlane.Application;

public sealed record NodeSummaryDto(
    Guid Id,
    string AgentIdentifier,
    string Name,
    string Cluster,
    string AgentBaseAddress,
    string Status,
    string? AgentVersion,
    DateTimeOffset? LastSeenAtUtc,
    int ActiveSessions,
    int EnabledPeerCount,
    string? LastError);

public sealed record SessionDto(
    Guid Id,
    Guid NodeId,
    Guid UserId,
    string NodeName,
    string UserDisplayName,
    string PublicKey,
    string? Endpoint,
    DateTimeOffset? ConnectedAtUtc,
    DateTimeOffset? LatestHandshakeAtUtc,
    long RxBytes,
    long TxBytes,
    string State);

public sealed record UserSummaryDto(
    Guid Id,
    string ExternalId,
    string DisplayName,
    string? Email,
    bool IsEnabled,
    int PeerCount,
    IReadOnlyList<Guid> NodeIds,
    IReadOnlyList<Guid> EnabledNodeIds,
    DateTimeOffset? LastActivityAtUtc);

public sealed record AccessSummaryDto(
    Guid Id,
    Guid NodeId,
    Guid UserId,
    string NodeName,
    string ExternalId,
    string DisplayName,
    string? Email,
    string PublicKey,
    string AllowedIps,
    string Protocol,
    bool IsEnabled,
    DateTimeOffset LastSyncedAtUtc,
    string? Endpoint,
    string SessionState,
    DateTimeOffset? LatestHandshakeAtUtc,
    DateTimeOffset? LastActivityAtUtc,
    string? AccountId,
    string? AccountEmail,
    string? AccountDisplayName,
    string? DeviceId,
    string? DeviceName,
    string? DevicePlatform,
    string? DeviceFingerprint,
    string? ClientVersion);

public sealed record IssuedNodeAccessDto(
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

public sealed record DeletedNodeAccessDto(
    Guid NodeId,
    Guid AccessId,
    Guid UserId,
    string PublicKey,
    bool UserDeleted);

public sealed record AccessConfigDto(
    Guid NodeId,
    Guid AccessId,
    Guid UserId,
    string PublicKey,
    string ClientConfigFileName,
    string ClientConfig);

public sealed record TrafficPointDto(
    DateTimeOffset CapturedAtUtc,
    string UserDisplayName,
    long RxBytes,
    long TxBytes);

public sealed record DashboardSnapshotDto(
    IReadOnlyList<NodeSummaryDto> Nodes,
    IReadOnlyList<SessionDto> Sessions,
    IReadOnlyList<UserSummaryDto> Users,
    IReadOnlyList<AccessSummaryDto> Accesses,
    IReadOnlyList<TrafficPointDto> Traffic);

public sealed record NodeRegistrationResult(
    Guid NodeId,
    string AgentIdentifier,
    string Name,
    string Cluster,
    string AgentBaseAddress,
    string Status);

public sealed record NodeRealtimeEnvelope(
    Guid NodeId,
    string NodeName,
    DateTimeOffset ObservedAtUtc,
    IReadOnlyList<SessionDto> Sessions);
