using Microsoft.Extensions.DependencyInjection;
using VpnControlPlane.Application.Abstractions;
using VpnControlPlane.Application.Dashboard.Queries;
using VpnControlPlane.Application.Nodes.Commands;
using VpnControlPlane.Application.Nodes.Queries;
using VpnControlPlane.Application.Users.Commands;
using VpnControlPlane.Application.Users.Queries;

namespace VpnControlPlane.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ICommandDispatcher, CommandDispatcher>();
        services.AddScoped<IQueryDispatcher, QueryDispatcher>();

        services.AddScoped<ICommandHandler<RegisterNodeCommand, NodeRegistrationResult>, RegisterNodeCommandHandler>();
        services.AddScoped<ICommandHandler<UpsertNodeSnapshotCommand, DashboardSnapshotDto>, UpsertNodeSnapshotCommandHandler>();
        services.AddScoped<ICommandHandler<CreateUserCommand, UserSummaryDto>, CreateUserCommandHandler>();

        services.AddScoped<IQueryHandler<GetNodesQuery, IReadOnlyList<NodeSummaryDto>>, GetNodesQueryHandler>();
        services.AddScoped<IQueryHandler<GetNodeSessionsQuery, IReadOnlyList<SessionDto>>, GetNodeSessionsQueryHandler>();
        services.AddScoped<IQueryHandler<GetUsersQuery, IReadOnlyList<UserSummaryDto>>, GetUsersQueryHandler>();
        services.AddScoped<IQueryHandler<GetDashboardQuery, DashboardSnapshotDto>, GetDashboardQueryHandler>();

        return services;
    }
}
