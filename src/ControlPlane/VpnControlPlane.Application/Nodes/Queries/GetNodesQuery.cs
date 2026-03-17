using VpnControlPlane.Application.Abstractions;

namespace VpnControlPlane.Application.Nodes.Queries;

public sealed record GetNodesQuery() : IQuery<IReadOnlyList<NodeSummaryDto>>;

public sealed class GetNodesQueryHandler(IDashboardReadService dashboardReadService)
    : IQueryHandler<GetNodesQuery, IReadOnlyList<NodeSummaryDto>>
{
    public Task<IReadOnlyList<NodeSummaryDto>> Handle(GetNodesQuery query, CancellationToken cancellationToken)
    {
        return dashboardReadService.GetNodesAsync(cancellationToken);
    }
}
