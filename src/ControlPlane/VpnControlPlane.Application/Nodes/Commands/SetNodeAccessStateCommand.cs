using System.Text.Json;
using VpnControlPlane.Application.Abstractions;
using VpnControlPlane.Contracts.Nodes;

namespace VpnControlPlane.Application.Nodes.Commands;

public sealed record SetNodeAccessStateCommand(
    Guid NodeId,
    Guid UserId,
    bool IsEnabled) : ICommand<UserSummaryDto>;

public sealed class SetNodeAccessStateCommandHandler(
    INodeRepository nodeRepository,
    INodeAgentClient nodeAgentClient,
    IDashboardReadService dashboardReadService,
    ICommandDispatcher commandDispatcher) : ICommandHandler<SetNodeAccessStateCommand, UserSummaryDto>
{
    public async Task<UserSummaryDto> Handle(SetNodeAccessStateCommand command, CancellationToken cancellationToken)
    {
        var node = await nodeRepository.GetByIdAsync(command.NodeId, includeRelated: true, cancellationToken)
            ?? throw new InvalidOperationException($"Node '{command.NodeId}' was not found.");

        var peerConfig = node.PeerConfigs
            .Where(x => x.UserId == command.UserId)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"User '{command.UserId}' has no access bound to node '{command.NodeId}'.");

        var metadata = PeerMetadataReader.Parse(peerConfig.MetadataJson);
        var payload = new SetAccessStateRequest(
            new AgentPeerMaterial(
                peerConfig.PublicKey,
                peerConfig.AllowedIps,
                metadata.PresharedKey,
                metadata.ClientPrivateKey,
                peerConfig.User.ExternalId,
                peerConfig.DisplayName,
                peerConfig.User.Email),
            command.IsEnabled,
            command.IsEnabled ? GetEndpointHost(node.AgentBaseAddress) : null);

        await nodeAgentClient.SetAccessStateAsync(node, payload, cancellationToken);

        try
        {
            var snapshot = await nodeAgentClient.GetSnapshotAsync(node, cancellationToken);
            await commandDispatcher.Send(new UpsertNodeSnapshotCommand(node.Id, snapshot), cancellationToken);
        }
        catch (Exception)
        {
            // Best effort refresh only. The background poller will reconcile the node state.
        }

        var users = await dashboardReadService.GetUsersAsync(cancellationToken);
        return users.FirstOrDefault(x => x.Id == command.UserId)
            ?? throw new InvalidOperationException($"User '{command.UserId}' was not found after access state update.");
    }

    private static string GetEndpointHost(string address)
    {
        return Uri.TryCreate(address, UriKind.Absolute, out var uri)
            ? uri.Host
            : address;
    }

    private sealed record PeerMetadata(string? PresharedKey, string? ClientPrivateKey);

    private static class PeerMetadataReader
    {
        public static PeerMetadata Parse(string? metadataJson)
        {
            if (string.IsNullOrWhiteSpace(metadataJson))
            {
                return new PeerMetadata(null, null);
            }

            using var document = JsonDocument.Parse(metadataJson);
            if (!document.RootElement.TryGetProperty("sources", out var sources) || sources.ValueKind != JsonValueKind.Array)
            {
                return new PeerMetadata(null, null);
            }

            string? presharedKey = null;
            string? clientPrivateKey = null;

            foreach (var source in sources.EnumerateArray())
            {
                if (presharedKey is null
                    && source.TryGetProperty("peerProperties", out var peerProperties)
                    && peerProperties.ValueKind == JsonValueKind.Object
                    && peerProperties.TryGetProperty("PresharedKey", out var presharedKeyElement))
                {
                    presharedKey = presharedKeyElement.GetString();
                }

                if (clientPrivateKey is null
                    && source.TryGetProperty("metadata", out var metadata)
                    && metadata.ValueKind == JsonValueKind.Object
                    && metadata.TryGetProperty("vpn-client-private-key", out var privateKeyElement))
                {
                    clientPrivateKey = privateKeyElement.GetString();
                }
            }

            return new PeerMetadata(presharedKey, clientPrivateKey);
        }
    }
}
