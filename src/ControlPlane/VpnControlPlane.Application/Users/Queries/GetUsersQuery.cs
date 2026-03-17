using VpnControlPlane.Application.Abstractions;

namespace VpnControlPlane.Application.Users.Queries;

public sealed record GetUsersQuery() : IQuery<IReadOnlyList<UserSummaryDto>>;

public sealed class GetUsersQueryHandler(IDashboardReadService dashboardReadService)
    : IQueryHandler<GetUsersQuery, IReadOnlyList<UserSummaryDto>>
{
    public Task<IReadOnlyList<UserSummaryDto>> Handle(GetUsersQuery query, CancellationToken cancellationToken)
    {
        return dashboardReadService.GetUsersAsync(cancellationToken);
    }
}
