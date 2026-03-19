using VpnControlPlane.Application.Abstractions;
using VpnControlPlane.Contracts.Nodes;

namespace VpnControlPlane.Application.Nodes.Commands;

public sealed record UpsertNodeSnapshotCommand(
    Guid NodeId,
    NodeSnapshotResponse Snapshot) : ICommand<DashboardSnapshotDto>;

public sealed class UpsertNodeSnapshotCommandHandler(
    INodeRepository nodeRepository,
    INodeSnapshotWriter snapshotWriter,
    IDashboardReadService dashboardReadService,
    ISessionRealtimeNotifier realtimeNotifier,
    IUnitOfWork unitOfWork) : ICommandHandler<UpsertNodeSnapshotCommand, DashboardSnapshotDto>
{
    public async Task<DashboardSnapshotDto> Handle(UpsertNodeSnapshotCommand command, CancellationToken cancellationToken)
    {
        var node = await nodeRepository.GetByIdAsync(command.NodeId, includeRelated: true, cancellationToken)
            ?? throw new InvalidOperationException($"Node '{command.NodeId}' was not found.");

        await snapshotWriter.ApplySnapshotAsync(node, command.Snapshot, cancellationToken);
        node.MarkHealthy(command.Snapshot.AgentVersion, command.Snapshot.CollectedAtUtc);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var nodes = await dashboardReadService.GetNodesAsync(cancellationToken);
        var sessions = await dashboardReadService.GetActiveSessionsAsync(command.NodeId, cancellationToken);
        var users = await dashboardReadService.GetUsersAsync(cancellationToken);
        var accesses = await dashboardReadService.GetAccessesAsync(command.NodeId, cancellationToken);
        var traffic = await dashboardReadService.GetTrafficPointsAsync(50, cancellationToken);

        await realtimeNotifier.PublishSnapshotAsync(
            new NodeRealtimeEnvelope(node.Id, node.Name, command.Snapshot.CollectedAtUtc, sessions),
            cancellationToken);

        return new DashboardSnapshotDto(nodes, sessions, users, accesses, traffic);
    }
}
