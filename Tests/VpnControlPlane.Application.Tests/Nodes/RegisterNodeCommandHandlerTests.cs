using VpnControlPlane.Application;
using VpnControlPlane.Application.Abstractions;
using VpnControlPlane.Application.Nodes.Commands;
using VpnControlPlane.Domain.Entities;

namespace VpnControlPlane.Application.Tests.Nodes;

public sealed class RegisterNodeCommandHandlerTests
{
    [Fact]
    public async Task Handle_CreatesNode_WhenAgentIdentifierIsNew()
    {
        var repository = new InMemoryNodeRepository();
        var unitOfWork = new FakeUnitOfWork();
        var clock = new FakeClock(new DateTimeOffset(2026, 3, 17, 12, 0, 0, TimeSpan.Zero));
        var handler = new RegisterNodeCommandHandler(repository, unitOfWork, clock);

        var result = await handler.Handle(
            new RegisterNodeCommand(
                "node-01",
                "Node 01",
                "eu-central",
                "https://10.0.0.5:8443",
                "ABC123",
                "Primary node"),
            CancellationToken.None);

        Assert.Equal("node-01", result.AgentIdentifier);
        Assert.Equal("Provisioning", result.Status);
        Assert.Single(repository.Nodes);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task Handle_UpdatesNode_WhenAgentIdentifierAlreadyExists()
    {
        var repository = new InMemoryNodeRepository();
        var existing = Node.Register(
            Guid.NewGuid(),
            "node-01",
            "Old",
            "cluster-a",
            "https://old-node:8443",
            null,
            null,
            new DateTimeOffset(2026, 3, 16, 12, 0, 0, TimeSpan.Zero));
        repository.Nodes.Add(existing);

        var unitOfWork = new FakeUnitOfWork();
        var clock = new FakeClock(new DateTimeOffset(2026, 3, 17, 12, 0, 0, TimeSpan.Zero));
        var handler = new RegisterNodeCommandHandler(repository, unitOfWork, clock);

        var result = await handler.Handle(
            new RegisterNodeCommand(
                "node-01",
                "Updated",
                "cluster-b",
                "https://new-node:8443",
                "DEF456",
                "Updated node"),
            CancellationToken.None);

        Assert.Equal(existing.Id, result.NodeId);
        Assert.Equal("Updated", existing.Name);
        Assert.Equal("cluster-b", existing.Cluster);
        Assert.Equal("https://new-node:8443", existing.AgentBaseAddress);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    private sealed class InMemoryNodeRepository : INodeRepository
    {
        public List<Node> Nodes { get; } = [];

        public Task AddAsync(Node node, CancellationToken cancellationToken)
        {
            Nodes.Add(node);
            return Task.CompletedTask;
        }

        public Task<Node?> GetByIdAsync(Guid id, bool includeRelated, CancellationToken cancellationToken)
        {
            return Task.FromResult(Nodes.FirstOrDefault(x => x.Id == id));
        }

        public Task<Node?> GetByAgentIdentifierAsync(string agentIdentifier, CancellationToken cancellationToken)
        {
            return Task.FromResult(Nodes.FirstOrDefault(x => x.AgentIdentifier == agentIdentifier));
        }

        public Task<IReadOnlyList<Node>> ListAsync(bool enabledOnly, CancellationToken cancellationToken)
        {
            IReadOnlyList<Node> result = enabledOnly ? Nodes.Where(x => x.IsEnabled).ToList() : Nodes.ToList();
            return Task.FromResult(result);
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCalls { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveChangesCalls++;
            return Task.FromResult(1);
        }
    }

    private sealed class FakeClock(DateTimeOffset value) : IClock
    {
        public DateTimeOffset UtcNow => value;
    }
}
