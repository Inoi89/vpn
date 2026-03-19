using VpnControlPlane.Application.Abstractions;
using VpnControlPlane.Application.Nodes;
using VpnControlPlane.Contracts.Nodes;

namespace VpnControlPlane.Application.Nodes.Commands;

public sealed record GetNodeAccessConfigCommand(
    Guid NodeId,
    Guid AccessId,
    string? Format) : ICommand<AccessConfigDto>;

public sealed class GetNodeAccessConfigCommandHandler(
    INodeRepository nodeRepository,
    INodeAgentClient nodeAgentClient) : ICommandHandler<GetNodeAccessConfigCommand, AccessConfigDto>
{
    public async Task<AccessConfigDto> Handle(GetNodeAccessConfigCommand command, CancellationToken cancellationToken)
    {
        var node = await nodeRepository.GetByIdAsync(command.NodeId, includeRelated: true, cancellationToken)
            ?? throw new InvalidOperationException($"Node '{command.NodeId}' was not found.");

        var peerConfig = node.PeerConfigs
            .FirstOrDefault(x => x.Id == command.AccessId)
            ?? throw new InvalidOperationException($"Access '{command.AccessId}' has no binding to node '{command.NodeId}'.");

        var metadata = PeerMetadataParser.Parse(peerConfig.MetadataJson);
        if (string.IsNullOrWhiteSpace(metadata.ClientPrivateKey))
        {
            metadata = await TryRefreshMetadataFromAgentAsync(node, peerConfig.PublicKey, metadata, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(metadata.ClientPrivateKey))
        {
            throw new InvalidOperationException(
                "Конфиг для этого доступа недоступен: приватный ключ не был сохранён. Для старых ключей нужно перевыпустить доступ.");
        }

        var payload = new GetAccessConfigRequest(
            new AgentPeerMaterial(
                peerConfig.PublicKey,
                peerConfig.AllowedIps,
                metadata.PresharedKey,
                metadata.ClientPrivateKey,
                peerConfig.User.ExternalId,
                peerConfig.DisplayName,
                peerConfig.User.Email),
            GetEndpointHost(node.AgentBaseAddress),
            command.Format);

        var result = await nodeAgentClient.GetAccessConfigAsync(node, payload, cancellationToken);
        return new AccessConfigDto(node.Id, peerConfig.Id, peerConfig.UserId, result.PublicKey, result.ClientConfigFileName, result.ClientConfig);
    }

    private async Task<PeerMetadataSnapshot> TryRefreshMetadataFromAgentAsync(
        Domain.Entities.Node node,
        string publicKey,
        PeerMetadataSnapshot current,
        CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await nodeAgentClient.GetSnapshotAsync(node, cancellationToken);
            var peerSnapshot = snapshot.PeerConfigs
                .FirstOrDefault(x => string.Equals(x.PublicKey, publicKey, StringComparison.OrdinalIgnoreCase));

            if (peerSnapshot is null)
            {
                return current;
            }

            var refreshed = PeerMetadataParser.Parse(peerSnapshot.MetadataJson);
            return refreshed with
            {
                PresharedKey = refreshed.PresharedKey ?? current.PresharedKey,
                ClientPrivateKey = refreshed.ClientPrivateKey ?? current.ClientPrivateKey,
                VpnUserExternalId = refreshed.VpnUserExternalId ?? current.VpnUserExternalId,
                VpnDisplayName = refreshed.VpnDisplayName ?? current.VpnDisplayName,
                VpnEmail = refreshed.VpnEmail ?? current.VpnEmail,
                IssuedAtUtc = refreshed.IssuedAtUtc ?? current.IssuedAtUtc,
                ProductAccountId = refreshed.ProductAccountId ?? current.ProductAccountId,
                ProductAccountEmail = refreshed.ProductAccountEmail ?? current.ProductAccountEmail,
                ProductAccountDisplayName = refreshed.ProductAccountDisplayName ?? current.ProductAccountDisplayName,
                ProductDeviceId = refreshed.ProductDeviceId ?? current.ProductDeviceId,
                ProductDeviceName = refreshed.ProductDeviceName ?? current.ProductDeviceName,
                ProductDevicePlatform = refreshed.ProductDevicePlatform ?? current.ProductDevicePlatform,
                ProductDeviceFingerprint = refreshed.ProductDeviceFingerprint ?? current.ProductDeviceFingerprint,
                ProductClientVersion = refreshed.ProductClientVersion ?? current.ProductClientVersion,
            };
        }
        catch
        {
            return current;
        }
    }

    private static string GetEndpointHost(string address)
    {
        return Uri.TryCreate(address, UriKind.Absolute, out var uri)
            ? uri.Host
            : address;
    }
}
