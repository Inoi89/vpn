using VpnControlPlane.Application.Abstractions;
using VpnControlPlane.Contracts.Nodes;
using VpnControlPlane.Domain.Entities;

namespace VpnControlPlane.Application.Nodes.Commands;

public sealed record IssueNodeAccessCommand(
    Guid NodeId,
    string DisplayName,
    string? Email) : ICommand<IssuedNodeAccessDto>;

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

        var externalId = $"issued-{Guid.NewGuid():N}";
        var issueResult = await nodeAgentClient.IssueAccessAsync(
            node,
            new IssueAccessRequest(
                externalId,
                command.DisplayName,
                command.Email,
                GetEndpointHost(node.AgentBaseAddress)),
            cancellationToken);

        var user = VpnUser.Create(Guid.NewGuid(), externalId, command.DisplayName, command.Email, isEnabled: true, clock.UtcNow);
        await userRepository.AddAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var snapshot = await nodeAgentClient.GetSnapshotAsync(node, cancellationToken);
        await commandDispatcher.Send(new UpsertNodeSnapshotCommand(node.Id, snapshot), cancellationToken);

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
