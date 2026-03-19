using VpnControlPlane.Contracts.Nodes;
using VpnControlPlane.Domain.Entities;

namespace VpnControlPlane.Application.Abstractions;

public interface INodeRepository
{
    Task AddAsync(Node node, CancellationToken cancellationToken);

    Task<Node?> GetByIdAsync(Guid id, bool includeRelated, CancellationToken cancellationToken);

    Task<Node?> GetByAgentIdentifierAsync(string agentIdentifier, CancellationToken cancellationToken);

    Task<IReadOnlyList<Node>> ListAsync(bool enabledOnly, CancellationToken cancellationToken);
}

public interface IUserRepository
{
    Task AddAsync(VpnUser user, CancellationToken cancellationToken);

    Task<VpnUser?> FindByExternalIdAsync(string externalId, CancellationToken cancellationToken);

    Task<VpnUser?> FindByEmailAsync(string email, CancellationToken cancellationToken);
}

public interface IAccessRepository
{
    Task<bool> DeleteNodeAccessAsync(Guid nodeId, Guid accessId, Guid userId, string publicKey, CancellationToken cancellationToken);
}

public interface IDashboardReadService
{
    Task<IReadOnlyList<NodeSummaryDto>> GetNodesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<SessionDto>> GetActiveSessionsAsync(Guid? nodeId, CancellationToken cancellationToken);

    Task<IReadOnlyList<UserSummaryDto>> GetUsersAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<AccessSummaryDto>> GetAccessesAsync(Guid? nodeId, CancellationToken cancellationToken);

    Task<IReadOnlyList<TrafficPointDto>> GetTrafficPointsAsync(int take, CancellationToken cancellationToken);
}

public interface INodeSnapshotWriter
{
    Task ApplySnapshotAsync(Node node, NodeSnapshotResponse snapshot, CancellationToken cancellationToken);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
