using VpnControlPlane.Application.Abstractions;

namespace VpnControlPlane.Application.Nodes.Queries;

public sealed record GetNodeSessionsQuery(Guid NodeId) : IQuery<IReadOnlyList<SessionDto>>;

public sealed class GetNodeSessionsQueryHandler(IDashboardReadService dashboardReadService)
    : IQueryHandler<GetNodeSessionsQuery, IReadOnlyList<SessionDto>>
{
    public Task<IReadOnlyList<SessionDto>> Handle(GetNodeSessionsQuery query, CancellationToken cancellationToken)
    {
        return dashboardReadService.GetActiveSessionsAsync(query.NodeId, cancellationToken);
    }
}
