using VpnControlPlane.Application.Abstractions;
using VpnControlPlane.Contracts.Nodes;

namespace VpnControlPlane.Application.Nodes.Commands;

public sealed record DeleteNodeAccessCommand(
    Guid NodeId,
    Guid UserId) : ICommand<DeletedNodeAccessDto>;

public sealed class DeleteNodeAccessCommandHandler(
    INodeRepository nodeRepository,
    IAccessRepository accessRepository,
    INodeAgentClient nodeAgentClient,
    IUnitOfWork unitOfWork) : ICommandHandler<DeleteNodeAccessCommand, DeletedNodeAccessDto>
{
    public async Task<DeletedNodeAccessDto> Handle(DeleteNodeAccessCommand command, CancellationToken cancellationToken)
    {
        var node = await nodeRepository.GetByIdAsync(command.NodeId, includeRelated: true, cancellationToken)
            ?? throw new InvalidOperationException($"Node '{command.NodeId}' was not found.");

        var peerConfig = node.PeerConfigs
            .Where(x => x.UserId == command.UserId)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .FirstOrDefault();

        if (peerConfig is null)
        {
            return new DeletedNodeAccessDto(node.Id, command.UserId, string.Empty, false);
        }

        await nodeAgentClient.DeleteAccessAsync(node, new DeleteAccessRequest(peerConfig.PublicKey), cancellationToken);
        var userDeleted = await accessRepository.DeleteNodeAccessAsync(node.Id, command.UserId, peerConfig.PublicKey, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new DeletedNodeAccessDto(
            node.Id,
            command.UserId,
            peerConfig.PublicKey,
            userDeleted);
    }
}
