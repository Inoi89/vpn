using Microsoft.AspNetCore.Authorization;
using VpnControlPlane.Contracts.Nodes;
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

        group.MapPost("/accesses/issue", async (IssueAccessRequest request, IAgentAccessService accessService, CancellationToken cancellationToken) =>
        {
            var result = await accessService.IssueAsync(request, cancellationToken);
            return Results.Ok(result);
        });

        group.MapPost("/accesses/state", async (SetAccessStateRequest request, IAgentAccessService accessService, CancellationToken cancellationToken) =>
        {
            var result = await accessService.SetStateAsync(request, cancellationToken);
            return Results.Ok(result);
        });

        group.MapPost("/accesses/delete", async (DeleteAccessRequest request, IAgentAccessService accessService, CancellationToken cancellationToken) =>
        {
            var result = await accessService.DeleteAsync(request, cancellationToken);
            return Results.Ok(result);
        });

        group.MapPost("/accesses/config", async (GetAccessConfigRequest request, IAgentAccessService accessService, CancellationToken cancellationToken) =>
        {
            var result = await accessService.GetConfigAsync(request, cancellationToken);
            return Results.Ok(result);
        });

        return endpoints;
    }
}
