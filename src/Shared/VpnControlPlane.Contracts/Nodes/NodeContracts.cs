using System;

namespace VpnControlPlane.Contracts.Nodes;

public static class AccessConfigFormats
{
    public const string AmneziaAwgNative = "amnezia-awg-native";

    public const string AmneziaVpn = "amnezia-vpn";

    public static string Normalize(string? format)
    {
        return string.Equals(format, AmneziaVpn, StringComparison.OrdinalIgnoreCase)
            ? AmneziaVpn
            : AmneziaAwgNative;
    }
}

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

public sealed record AgentPeerMaterial(
    string PublicKey,
    string AllowedIps,
    string? PresharedKey,
    string? ClientPrivateKey,
    string UserExternalId,
    string DisplayName,
    string? UserEmail,
    ProductPeerMetadata? ProductMetadata);

public sealed record ProductPeerMetadata(
    string? AccountId,
    string? AccountEmail,
    string? AccountDisplayName,
    string? DeviceId,
    string? DeviceName,
    string? DevicePlatform,
    string? DeviceFingerprint,
    string? ClientVersion);

public sealed record IssueAccessRequest(
    string UserExternalId,
    string DisplayName,
    string? UserEmail,
    string EndpointHost,
    string? Format,
    ProductPeerMetadata? ProductMetadata);

public sealed record IssueAccessResponse(
    AgentPeerMaterial Peer,
    string ClientConfigFileName,
    string ClientConfig);

public sealed record SetAccessStateRequest(
    AgentPeerMaterial Peer,
    bool IsEnabled,
    string? EndpointHost);

public sealed record SetAccessStateResponse(
    string PublicKey,
    bool IsEnabled,
    string? ClientConfigFileName,
    string? ClientConfig);

public sealed record DeleteAccessRequest(string PublicKey);

public sealed record DeleteAccessResponse(
    string PublicKey,
    bool Deleted);

public sealed record GetAccessConfigRequest(
    AgentPeerMaterial Peer,
    string EndpointHost,
    string? Format);

public sealed record GetAccessConfigResponse(
    string PublicKey,
    string ClientConfigFileName,
    string ClientConfig);
