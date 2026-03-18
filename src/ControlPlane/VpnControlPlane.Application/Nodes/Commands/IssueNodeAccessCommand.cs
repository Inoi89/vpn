using VpnControlPlane.Application.Abstractions;
using VpnControlPlane.Contracts.Nodes;
using VpnControlPlane.Domain.Entities;

namespace VpnControlPlane.Application.Nodes.Commands;

public sealed record IssueNodeAccessCommand(
    Guid NodeId,
    string DisplayName,
    string? Email,
    string? Format) : ICommand<IssuedNodeAccessDto>;

public sealed class IssueNodeAccessCommandHandler(
    INodeRepository nodeRepository,
    IUserRepository userRepository,
    INodeAgentClient nodeAgentClient,
    ICommandDispatcher commandDispatcher,
    IUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<IssueNodeAccessCommand, IssuedNodeAccessDto>
{
    public async Task<IssuedNodeAccessDto> Handle(IssueNodeAccessCommand command, CancellationToken cancellationToken)
    {
        var node = await nodeRepository.GetByIdAsync(command.NodeId, includeRelated: false, cancellationToken)
            ?? throw new InvalidOperationException($"Node '{command.NodeId}' was not found.");

        var issueResult = await nodeAgentClient.IssueAccessAsync(
            node,
            new IssueAccessRequest(
                $"issued-{Guid.NewGuid():N}",
                command.DisplayName,
                command.Email,
                GetEndpointHost(node.AgentBaseAddress),
                command.Format),
            cancellationToken);

        var user = VpnUser.Create(
            Guid.NewGuid(),
            issueResult.Peer.UserExternalId,
            command.DisplayName,
            command.Email,
            isEnabled: true,
            clock.UtcNow);
        await userRepository.AddAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            var snapshot = await nodeAgentClient.GetSnapshotAsync(node, cancellationToken);
            await commandDispatcher.Send(new UpsertNodeSnapshotCommand(node.Id, snapshot), cancellationToken);
        }
        catch (Exception)
        {
            // Best effort refresh only. The background poller will reconcile the node state.
        }

        return new IssuedNodeAccessDto(
            node.Id,
            user.Id,
            user.ExternalId,
            user.DisplayName,
            user.Email,
            issueResult.Peer.PublicKey,
            issueResult.Peer.AllowedIps,
            issueResult.ClientConfigFileName,
            issueResult.ClientConfig);
    }

    private static string GetEndpointHost(string address)
    {
        return Uri.TryCreate(address, UriKind.Absolute, out var uri)
            ? uri.Host
            : address;
    }
}
