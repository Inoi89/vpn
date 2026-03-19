using VpnControlPlane.Application.Abstractions;
using VpnControlPlane.Application.Nodes;
using VpnControlPlane.Contracts.Nodes;

namespace VpnControlPlane.Application.Nodes.Commands;

public sealed record SetNodeAccessStateCommand(
    Guid NodeId,
    Guid AccessId,
    bool IsEnabled) : ICommand<AccessSummaryDto>;

public sealed class SetNodeAccessStateCommandHandler(
    INodeRepository nodeRepository,
    INodeAgentClient nodeAgentClient,
    IDashboardReadService dashboardReadService,
    ICommandDispatcher commandDispatcher) : ICommandHandler<SetNodeAccessStateCommand, AccessSummaryDto>
{
    public async Task<AccessSummaryDto> Handle(SetNodeAccessStateCommand command, CancellationToken cancellationToken)
    {
        var node = await nodeRepository.GetByIdAsync(command.NodeId, includeRelated: true, cancellationToken)
            ?? throw new InvalidOperationException($"Node '{command.NodeId}' was not found.");

        var peerConfig = node.PeerConfigs
            .FirstOrDefault(x => x.Id == command.AccessId)
            ?? throw new InvalidOperationException($"Access '{command.AccessId}' has no binding to node '{command.NodeId}'.");

        var metadata = PeerMetadataParser.Parse(peerConfig.MetadataJson);
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

        var accesses = await dashboardReadService.GetAccessesAsync(command.NodeId, cancellationToken);
        return accesses.FirstOrDefault(x => x.Id == command.AccessId)
            ?? throw new InvalidOperationException($"Access '{command.AccessId}' was not found after access state update.");
    }

    private static string GetEndpointHost(string address)
    {
        return Uri.TryCreate(address, UriKind.Absolute, out var uri)
            ? uri.Host
            : address;
    }
}
