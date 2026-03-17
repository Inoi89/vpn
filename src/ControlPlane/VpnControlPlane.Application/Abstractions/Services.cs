using VpnControlPlane.Contracts.Nodes;
using VpnControlPlane.Domain.Entities;

namespace VpnControlPlane.Application.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public interface INodeAgentClient
{
    Task<NodeSnapshotResponse> GetSnapshotAsync(Node node, CancellationToken cancellationToken);

    Task<IssueAccessResponse> IssueAccessAsync(Node node, IssueAccessRequest request, CancellationToken cancellationToken);

    Task<SetAccessStateResponse> SetAccessStateAsync(Node node, SetAccessStateRequest request, CancellationToken cancellationToken);
}

public interface ISessionRealtimeNotifier
{
    Task PublishSnapshotAsync(NodeRealtimeEnvelope snapshot, CancellationToken cancellationToken);
}
