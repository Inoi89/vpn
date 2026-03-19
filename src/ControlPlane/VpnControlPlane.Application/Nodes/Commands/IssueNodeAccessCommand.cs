using VpnControlPlane.Application.Abstractions;
using VpnControlPlane.Contracts.Nodes;
using VpnControlPlane.Domain.Entities;

namespace VpnControlPlane.Application.Nodes.Commands;

public sealed record IssueNodeAccessCommand(
    Guid NodeId,
    string DisplayName,
    string? Email,
    string? Format,
    ProductPeerMetadata? ProductMetadata) : ICommand<IssuedNodeAccessDto>;

public sealed class IssueNodeAccessCommandHandler(
    INodeRepository nodeRepository,
    IUserRepository userRepository,
    IAccessRepository accessRepository,
    INodeAgentClient nodeAgentClient,
    ICommandDispatcher commandDispatcher,
    IUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<IssueNodeAccessCommand, IssuedNodeAccessDto>
{
    public async Task<IssuedNodeAccessDto> Handle(IssueNodeAccessCommand command, CancellationToken cancellationToken)
    {
        var node = await nodeRepository.GetByIdAsync(command.NodeId, includeRelated: false, cancellationToken)
            ?? throw new InvalidOperationException($"Node '{command.NodeId}' was not found.");

        var externalId = BuildUserExternalId(command.ProductMetadata);
        var issueResult = await nodeAgentClient.IssueAccessAsync(
            node,
            new IssueAccessRequest(
                externalId,
                command.DisplayName,
                command.Email,
                GetEndpointHost(node.AgentBaseAddress),
                command.Format,
                command.ProductMetadata),
            cancellationToken);

        var user = await userRepository.FindByExternalIdAsync(issueResult.Peer.UserExternalId, cancellationToken);
        if (user is null && !string.IsNullOrWhiteSpace(command.Email))
        {
            user = await userRepository.FindByEmailAsync(command.Email, cancellationToken);
        }

        if (user is null)
        {
            user = VpnUser.Create(
                Guid.NewGuid(),
                issueResult.Peer.UserExternalId,
                command.DisplayName,
                command.Email,
                isEnabled: true,
                clock.UtcNow);
            await userRepository.AddAsync(user, cancellationToken);
        }
        else
        {
            user.UpdateProfile(command.DisplayName, command.Email, isEnabled: true, clock.UtcNow);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        Guid? accessId = null;
        try
        {
            var snapshot = await nodeAgentClient.GetSnapshotAsync(node, cancellationToken);
            await commandDispatcher.Send(new UpsertNodeSnapshotCommand(node.Id, snapshot), cancellationToken);
            accessId = (await accessRepository.FindNodeAccessByPublicKeyAsync(node.Id, issueResult.Peer.PublicKey, cancellationToken))?.AccessId;
        }
        catch (Exception)
        {
            // Best effort refresh only. The background poller will reconcile the node state.
        }

        return new IssuedNodeAccessDto(
            node.Id,
            accessId,
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

    private static string BuildUserExternalId(ProductPeerMetadata? productMetadata)
    {
        if (!string.IsNullOrWhiteSpace(productMetadata?.DeviceId))
        {
            return $"product-device-{productMetadata.DeviceId.Trim()}";
        }

        if (!string.IsNullOrWhiteSpace(productMetadata?.AccountId))
        {
            return $"product-account-{productMetadata.AccountId.Trim()}";
        }

        return $"issued-{Guid.NewGuid():N}";
    }
}
