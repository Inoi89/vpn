using VpnProductPlatform.Domain.Entities;

namespace VpnProductPlatform.Application.Abstractions;

public interface IAccountRepository
{
    Task AddAsync(Account account, CancellationToken cancellationToken);
    Task<Account?> FindByEmailAsync(string email, CancellationToken cancellationToken);
    Task<Account?> GetByIdAsync(Guid accountId, CancellationToken cancellationToken);
}

public interface IDeviceRepository
{
    Task AddAsync(Device device, CancellationToken cancellationToken);
    Task<Device?> FindByFingerprintAsync(Guid accountId, string fingerprint, CancellationToken cancellationToken);
    Task<Device?> GetByIdAsync(Guid deviceId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Device>> ListByAccountIdAsync(Guid accountId, CancellationToken cancellationToken);
    Task<int> CountActiveByAccountIdAsync(Guid accountId, CancellationToken cancellationToken);
}

public interface ISubscriptionRepository
{
    Task AddAsync(Subscription subscription, CancellationToken cancellationToken);
    Task<Subscription?> GetActiveByAccountIdAsync(Guid accountId, DateTimeOffset now, CancellationToken cancellationToken);
    Task<SubscriptionPlan?> GetDefaultPlanAsync(CancellationToken cancellationToken);
    Task AddPlanAsync(SubscriptionPlan plan, CancellationToken cancellationToken);
}

public interface IAccountSessionRepository
{
    Task AddAsync(AccountSession session, CancellationToken cancellationToken);
    Task<AccountSession?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AccountSession>> ListByAccountIdAsync(Guid accountId, CancellationToken cancellationToken);
}

public interface IAccessGrantRepository
{
    Task AddAsync(AccessGrant accessGrant, CancellationToken cancellationToken);
    Task<AccessGrant?> GetActiveByDeviceIdAsync(Guid accountId, Guid deviceId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AccessGrant>> ListByAccountIdAsync(Guid accountId, CancellationToken cancellationToken);
}

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
