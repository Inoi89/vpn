using VpnControlPlane.Application.Abstractions;

namespace VpnControlPlane.Application.Dashboard.Queries;

public sealed record GetDashboardQuery(int TrafficPoints = 100) : IQuery<DashboardSnapshotDto>;

public sealed class GetDashboardQueryHandler(IDashboardReadService dashboardReadService)
    : IQueryHandler<GetDashboardQuery, DashboardSnapshotDto>
{
    public async Task<DashboardSnapshotDto> Handle(GetDashboardQuery query, CancellationToken cancellationToken)
    {
        var nodes = await dashboardReadService.GetNodesAsync(cancellationToken);
        var sessions = await dashboardReadService.GetActiveSessionsAsync(nodeId: null, cancellationToken);
        var users = await dashboardReadService.GetUsersAsync(cancellationToken);
        var accesses = await dashboardReadService.GetAccessesAsync(nodeId: null, cancellationToken);
        var traffic = await dashboardReadService.GetTrafficPointsAsync(query.TrafficPoints, cancellationToken);

        return new DashboardSnapshotDto(nodes, sessions, users, accesses, traffic);
    }
}
