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

    [HttpPost("{nodeId:guid}/accesses")]
    public async Task<ActionResult<VpnControlPlane.Application.IssuedNodeAccessDto>> IssueNodeAccess(
        Guid nodeId,
        [FromBody] IssueNodeAccessRequest request,
        CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.Send(
            new IssueNodeAccessCommand(nodeId, request.DisplayName, request.Email, request.ConfigFormat),
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("{nodeId:guid}/accesses/{userId:guid}/state")]
    public async Task<ActionResult<VpnControlPlane.Application.UserSummaryDto>> SetNodeAccessState(
        Guid nodeId,
        Guid userId,
        [FromBody] NodeAccessStateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.Send(
            new SetNodeAccessStateCommand(nodeId, userId, request.IsEnabled),
            cancellationToken);

        return Ok(result);
    }

    [HttpDelete("{nodeId:guid}/accesses/{userId:guid}")]
    public async Task<ActionResult<VpnControlPlane.Application.DeletedNodeAccessDto>> DeleteNodeAccess(
        Guid nodeId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.Send(
            new DeleteNodeAccessCommand(nodeId, userId),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{nodeId:guid}/accesses/{userId:guid}/config")]
    public async Task<ActionResult<VpnControlPlane.Application.AccessConfigDto>> GetNodeAccessConfig(
        Guid nodeId,
        Guid userId,
        [FromQuery] string? format,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await commandDispatcher.Send(
                new GetNodeAccessConfigCommand(nodeId, userId, format),
                cancellationToken);

            return Ok(result);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { error = exception.Message });
        }
    }
}

public sealed record IssueNodeAccessRequest(
    string DisplayName,
    string? Email,
    string? ConfigFormat);

public sealed record NodeAccessStateRequest(bool IsEnabled);
