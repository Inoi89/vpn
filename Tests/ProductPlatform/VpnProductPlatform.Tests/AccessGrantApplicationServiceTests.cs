using VpnProductPlatform.Application.Abstractions;
using VpnProductPlatform.Application.AccessGrants;
using VpnProductPlatform.Contracts;
using VpnProductPlatform.Domain.Entities;
using VpnProductPlatform.Domain.Enums;

namespace VpnProductPlatform.Tests;

public sealed class AccessGrantApplicationServiceTests
{
    [Fact]
    public async Task ListAsync_ReturnsDeviceBoundGrantWithAllowedIps()
    {
        var fixture = Fixture.Create();
        var grant = fixture.CreateActiveGrant();

        var grants = await fixture.Service.ListAsync(fixture.Account.Id, CancellationToken.None);

        var result = Assert.Single(grants);
        Assert.Equal(grant.Id, result.AccessGrantId);
        Assert.Equal("10.8.1.8/32", result.AllowedIps);
        Assert.Equal("Active", result.Status);
    }

    [Fact]
    public async Task IssueAsync_ProvisionsGrant_ForDeviceAndNode()
    {
        var fixture = Fixture.Create();

        var result = await fixture.Service.IssueAsync(
            fixture.Account.Id,
            new IssueAccessGrantRequest(fixture.Device.Id, fixture.Node.NodeId, "amnezia-vpn"),
            CancellationToken.None);

        Assert.Equal(fixture.Device.Id, result.DeviceId);
        Assert.Equal(fixture.Node.NodeId, result.NodeId);
        Assert.Equal(fixture.Device.DeviceName, result.DeviceName);
        Assert.Equal("pub-key-1", result.PeerPublicKey);
        Assert.Equal("10.8.1.9/32", result.AllowedIps);
        Assert.Equal("amnezia-vpn", result.ConfigFormat);
        Assert.Equal("device.vpn", result.ClientConfigFileName);
        Assert.Contains("[Interface]", result.ClientConfig, StringComparison.Ordinal);
        Assert.Single(fixture.AccessGrantRepository.Items.Where(x => x.Status == AccessGrantStatus.Active));
    }

    [Fact]
    public async Task IssueAsync_ReturnsExistingGrantConfig_ForSameDevice()
    {
        var fixture = Fixture.Create();
        var grant = fixture.CreateActiveGrant();

        var result = await fixture.Service.IssueAsync(
            fixture.Account.Id,
            new IssueAccessGrantRequest(fixture.Device.Id, fixture.Node.NodeId, "amnezia-vpn"),
            CancellationToken.None);

        Assert.Equal(grant.Id, result.AccessGrantId);
        Assert.Equal(grant.ControlPlaneAccessId, result.ControlPlaneAccessId);
        Assert.Equal("restored.vpn", result.ClientConfigFileName);
        Assert.Contains("Address = 10.8.1.8/32", result.ClientConfig, StringComparison.Ordinal);
    }

    private sealed class Fixture
    {
        private Fixture(
            AccessGrantApplicationService service,
            FakeClock clock,
            InMemoryAccountRepository accountRepository,
            InMemoryDeviceRepository deviceRepository,
            InMemorySubscriptionRepository subscriptionRepository,
            InMemoryAccessGrantRepository accessGrantRepository,
            FakeControlPlaneProvisioningClient controlPlane,
            Account account,
            Device device,
            ControlPlaneNodeEnvelope node)
        {
            Service = service;
            Clock = clock;
            AccountRepository = accountRepository;
            DeviceRepository = deviceRepository;
            SubscriptionRepository = subscriptionRepository;
            AccessGrantRepository = accessGrantRepository;
            ControlPlane = controlPlane;
            Account = account;
            Device = device;
            Node = node;
        }

        public AccessGrantApplicationService Service { get; }
        public FakeClock Clock { get; }
        public InMemoryAccountRepository AccountRepository { get; }
        public InMemoryDeviceRepository DeviceRepository { get; }
        public InMemorySubscriptionRepository SubscriptionRepository { get; }
        public InMemoryAccessGrantRepository AccessGrantRepository { get; }
        public FakeControlPlaneProvisioningClient ControlPlane { get; }
        public Account Account { get; }
        public Device Device { get; }
        public ControlPlaneNodeEnvelope Node { get; }

        public static Fixture Create()
        {
            var now = new DateTimeOffset(2026, 3, 19, 12, 0, 0, TimeSpan.Zero);
            var clock = new FakeClock(now);
            var account = Account.Create(Guid.NewGuid(), "alex@example.com", "Alex", "hash", now);
            account.VerifyEmail(now);
            var device = Device.Create(Guid.NewGuid(), account.Id, "Alex-PC", "windows", "fp-1", "0.1.7", now);
            var node = new ControlPlaneNodeEnvelope(Guid.NewGuid(), "Amnezia 5.61.37.29", "Healthy", 4, 11);

            var accountRepository = new InMemoryAccountRepository(account);
            var deviceRepository = new InMemoryDeviceRepository(device);
            var subscriptionRepository = new InMemorySubscriptionRepository(account.Id, now);
            var accessGrantRepository = new InMemoryAccessGrantRepository(device);
            var controlPlane = new FakeControlPlaneProvisioningClient(node);
            var unitOfWork = new FakeUnitOfWork();

            var service = new AccessGrantApplicationService(
                accountRepository,
                deviceRepository,
                subscriptionRepository,
                accessGrantRepository,
                controlPlane,
                unitOfWork,
                clock);

            return new Fixture(
                service,
                clock,
                accountRepository,
                deviceRepository,
                subscriptionRepository,
                accessGrantRepository,
                controlPlane,
                account,
                device,
                node);
        }

        public AccessGrant CreateActiveGrant()
        {
            var grant = AccessGrant.Create(
                Guid.NewGuid(),
                Account.Id,
                Device.Id,
                Node.NodeId,
                Guid.NewGuid(),
                "pub-key-existing",
                "10.8.1.8/32",
                "amnezia-vpn",
                Clock.UtcNow,
                Clock.UtcNow.AddDays(30),
                Clock.UtcNow);
            grant.Activate(Node.NodeId, grant.ControlPlaneAccessId, grant.PeerPublicKey, grant.AllowedIps, Clock.UtcNow);
            AccessGrantRepository.Items.Add(grant);

            typeof(AccessGrant)
                .GetProperty(nameof(AccessGrant.Device))!
                .SetValue(grant, Device);

            return grant;
        }
    }

    private sealed class FakeClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; private set; } = utcNow;
    }

    private sealed class InMemoryAccountRepository(Account account) : IAccountRepository
    {
        public Task AddAsync(Account storedAccount, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Account?> FindByEmailAsync(string email, CancellationToken cancellationToken)
        {
            return Task.FromResult<Account?>(string.Equals(account.Email, email.Trim().ToLowerInvariant(), StringComparison.Ordinal)
                ? account
                : null);
        }

        public Task<Account?> GetByIdAsync(Guid accountId, CancellationToken cancellationToken)
        {
            return Task.FromResult<Account?>(account.Id == accountId ? account : null);
        }
    }

    private sealed class InMemoryDeviceRepository(Device device) : IDeviceRepository
    {
        public Task AddAsync(Device value, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Device?> FindByFingerprintAsync(Guid accountId, string fingerprint, CancellationToken cancellationToken)
        {
            return Task.FromResult<Device?>(device.AccountId == accountId && device.Fingerprint == fingerprint ? device : null);
        }

        public Task<Device?> GetByIdAsync(Guid deviceId, CancellationToken cancellationToken)
        {
            return Task.FromResult<Device?>(device.Id == deviceId ? device : null);
        }

        public Task<IReadOnlyList<Device>> ListByAccountIdAsync(Guid accountId, CancellationToken cancellationToken)
        {
            IReadOnlyList<Device> result = device.AccountId == accountId ? [device] : [];
            return Task.FromResult(result);
        }

        public Task<int> CountActiveByAccountIdAsync(Guid accountId, CancellationToken cancellationToken)
        {
            return Task.FromResult(device.AccountId == accountId && device.Status == DeviceStatus.Active ? 1 : 0);
        }
    }

    private sealed class InMemorySubscriptionRepository : ISubscriptionRepository
    {
        private readonly SubscriptionPlan _plan;
        private readonly Subscription _subscription;

        public InMemorySubscriptionRepository(Guid accountId, DateTimeOffset now)
        {
            _plan = SubscriptionPlan.Create(
                Guid.NewGuid(),
                "Trial",
                maxDevices: 2,
                maxConcurrentSessions: 1,
                priceAmount: 0m,
                currency: "USD",
                billingPeriodMonths: 1,
                isActive: true,
                now);

            _subscription = Subscription.CreateTrial(
                Guid.NewGuid(),
                accountId,
                _plan.Id,
                now,
                TimeSpan.FromDays(30));
            typeof(Subscription)
                .GetProperty(nameof(Subscription.Plan))!
                .SetValue(_subscription, _plan);
        }

        public Task AddAsync(Subscription subscription, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Subscription?> GetActiveByAccountIdAsync(Guid requestAccountId, DateTimeOffset current, CancellationToken cancellationToken)
        {
            return Task.FromResult<Subscription?>(_subscription.AccountId == requestAccountId && _subscription.IsActiveAt(current) ? _subscription : null);
        }

        public Task<SubscriptionPlan?> GetDefaultPlanAsync(CancellationToken cancellationToken) => Task.FromResult<SubscriptionPlan?>(_plan);

        public Task AddPlanAsync(SubscriptionPlan plan, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class InMemoryAccessGrantRepository(Device device) : IAccessGrantRepository
    {
        public List<AccessGrant> Items { get; } = [];

        public Task AddAsync(AccessGrant accessGrant, CancellationToken cancellationToken)
        {
            typeof(AccessGrant)
                .GetProperty(nameof(AccessGrant.Device))!
                .SetValue(accessGrant, device);
            Items.Add(accessGrant);
            return Task.CompletedTask;
        }

        public Task<AccessGrant?> GetActiveByDeviceIdAsync(Guid accountId, Guid deviceId, CancellationToken cancellationToken)
        {
            var grant = Items
                .Where(x => x.AccountId == accountId && x.DeviceId == deviceId && x.Status == AccessGrantStatus.Active)
                .OrderByDescending(x => x.IssuedAtUtc)
                .FirstOrDefault();
            return Task.FromResult(grant);
        }

        public Task<IReadOnlyList<AccessGrant>> ListByAccountIdAsync(Guid accountId, CancellationToken cancellationToken)
        {
            IReadOnlyList<AccessGrant> grants = Items.Where(x => x.AccountId == accountId).ToList();
            return Task.FromResult(grants);
        }
    }

    private sealed class FakeControlPlaneProvisioningClient(ControlPlaneNodeEnvelope node) : IControlPlaneProvisioningClient
    {
        public Task<IReadOnlyList<ControlPlaneNodeEnvelope>> ListNodesAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<ControlPlaneNodeEnvelope> nodes = [node];
            return Task.FromResult(nodes);
        }

        public Task<ControlPlaneIssuedAccessEnvelope> IssueAccessAsync(ControlPlaneIssueAccessRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(
                new ControlPlaneIssuedAccessEnvelope(
                    request.NodeId,
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    $"product-device-{request.ProductMetadata.DeviceId:N}",
                    request.DisplayName,
                    request.Email,
                    "pub-key-1",
                    "10.8.1.9/32",
                    "device.vpn",
                    "[Interface]\nAddress = 10.8.1.9/32"));
        }

        public Task<ControlPlaneAccessConfigEnvelope> GetAccessConfigAsync(Guid nodeId, Guid accessId, string configFormat, CancellationToken cancellationToken)
        {
            return Task.FromResult(
                new ControlPlaneAccessConfigEnvelope(
                    nodeId,
                    accessId,
                    configFormat,
                    "restored.vpn",
                    "[Interface]\nAddress = 10.8.1.8/32"));
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
