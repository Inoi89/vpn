using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VpnControlPlane.Application.Abstractions;
using VpnControlPlane.Application.Nodes.Commands;
using VpnControlPlane.Application.Nodes.Queries;
using VpnControlPlane.Contracts.Nodes;

namespace VpnControlPlane.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/nodes")]
public sealed class NodesController(ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<VpnControlPlane.Application.NodeSummaryDto>>> GetNodes(CancellationToken cancellationToken)
    {
        var nodes = await queryDispatcher.Query(new GetNodesQuery(), cancellationToken);
        return Ok(nodes);
    }

    [HttpGet("{nodeId:guid}/sessions")]
    public async Task<ActionResult<IReadOnlyList<VpnControlPlane.Application.SessionDto>>> GetNodeSessions(Guid nodeId, CancellationToken cancellationToken)
    {
        var sessions = await queryDispatcher.Query(new GetNodeSessionsQuery(nodeId), cancellationToken);
        return Ok(sessions);
    }

    [HttpPost("register")]
    public async Task<ActionResult<NodeRegistrationResponse>> RegisterNode(
        [FromBody] RegisterNodeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.Send(
            new RegisterNodeCommand(
                request.AgentIdentifier,
                request.Name,
                request.Cluster,
                request.AgentBaseAddress,
                request.CertificateThumbprint,
                request.Description),
            cancellationToken);

        var response = new NodeRegistrationResponse(
            result.NodeId,
            result.AgentIdentifier,
            result.Name,
            result.Cluster,
            result.AgentBaseAddress,
            result.Status);

        return CreatedAtAction(nameof(GetNodes), new { id = response.NodeId }, response);
    }
}
