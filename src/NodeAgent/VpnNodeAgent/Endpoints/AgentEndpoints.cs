using Microsoft.AspNetCore.Authorization;
using VpnNodeAgent.Abstractions;

namespace VpnNodeAgent.Endpoints;

public static class AgentEndpoints
{
    public static IEndpointRouteBuilder MapAgentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/v1/agent").RequireAuthorization();

        group.MapGet("/snapshot", async (IAgentSnapshotService snapshotService, CancellationToken cancellationToken) =>
        {
            var snapshot = await snapshotService.BuildSnapshotAsync(cancellationToken);
            return Results.Ok(snapshot);
        });

        return endpoints;
    }
}
