using VpnProductPlatform.Application.AccessGrants;
using VpnProductPlatform.Application.Abstractions;
using VpnProductPlatform.Domain.Entities;

namespace VpnProductPlatform.Tests;

public sealed class AccessGrantApplicationServiceTests
{
    [Fact]
    public async Task ListAsync_ReturnsDeviceBoundGrants()
    {
        var now = new DateTimeOffset(2026, 3, 19, 12, 0, 0, TimeSpan.Zero);
        var accountId = Guid.NewGuid();
        var device = Device.Create(Guid.NewGuid(), accountId, "Alex-PC", "windows", "fp-1", "0.1.4", now);
        var grant = AccessGrant.Create(
            Guid.NewGuid(),
            accountId,
            device.Id,
            Guid.NewGuid(),
            "pub-key",
            "amnezia-vpn",
            now,
            now.AddDays(30),
            now);
        grant.Activate(grant.NodeId, grant.PeerPublicKey, now);

        typeof(AccessGrant)
            .GetProperty(nameof(AccessGrant.Device))!
            .SetValue(grant, device);

        var service = new AccessGrantApplicationService(new FakeAccessGrantRepository(grant));

        var grants = await service.ListAsync(accountId, CancellationToken.None);

        var result = Assert.Single(grants);
        Assert.Equal(device.DeviceName, result.DeviceName);
        Assert.Equal("Active", result.Status);
        Assert.Equal("amnezia-vpn", result.ConfigFormat);
    }

    private sealed class FakeAccessGrantRepository(params AccessGrant[] grants) : IAccessGrantRepository
    {
        private readonly List<AccessGrant> _items = grants.ToList();

        public Task AddAsync(AccessGrant accessGrant, CancellationToken cancellationToken)
        {
            _items.Add(accessGrant);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AccessGrant>> ListByAccountIdAsync(Guid accountId, CancellationToken cancellationToken)
        {
            IReadOnlyList<AccessGrant> grants = _items.Where(x => x.AccountId == accountId).ToList();
            return Task.FromResult(grants);
        }
    }
}
