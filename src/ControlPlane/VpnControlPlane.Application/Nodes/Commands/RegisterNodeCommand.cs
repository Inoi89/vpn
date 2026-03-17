using VpnControlPlane.Application.Abstractions;
using VpnControlPlane.Domain.Entities;

namespace VpnControlPlane.Application.Nodes.Commands;

public sealed record RegisterNodeCommand(
    string AgentIdentifier,
    string Name,
    string Cluster,
    string AgentBaseAddress,
    string? CertificateThumbprint,
    string? Description) : ICommand<NodeRegistrationResult>;

public sealed class RegisterNodeCommandHandler(
    INodeRepository nodeRepository,
    IUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<RegisterNodeCommand, NodeRegistrationResult>
{
    public async Task<NodeRegistrationResult> Handle(RegisterNodeCommand command, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var existing = await nodeRepository.GetByAgentIdentifierAsync(command.AgentIdentifier, cancellationToken);

        if (existing is null)
        {
            existing = Node.Register(
                Guid.NewGuid(),
                command.AgentIdentifier,
                command.Name,
                command.Cluster,
                command.AgentBaseAddress,
                command.CertificateThumbprint,
                command.Description,
                now);

            await nodeRepository.AddAsync(existing, cancellationToken);
        }
        else
        {
            existing.UpdateRegistration(
                command.Name,
                command.Cluster,
                command.AgentBaseAddress,
                command.CertificateThumbprint,
                command.Description,
                now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new NodeRegistrationResult(
            existing.Id,
            existing.AgentIdentifier,
            existing.Name,
            existing.Cluster,
            existing.AgentBaseAddress,
            existing.Status.ToString());
    }
}
