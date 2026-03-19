using Microsoft.EntityFrameworkCore;
using VpnProductPlatform.Application.Abstractions;
using VpnProductPlatform.Domain.Entities;
using VpnProductPlatform.Domain.Enums;

namespace VpnProductPlatform.Infrastructure.Persistence;

internal sealed class EfAccountRepository(ProductPlatformDbContext dbContext) : IAccountRepository
{
    public Task AddAsync(Account account, CancellationToken cancellationToken)
    {
        return dbContext.Accounts.AddAsync(account, cancellationToken).AsTask();
    }

    public Task<Account?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return dbContext.Accounts.FirstOrDefaultAsync(x => x.Email == normalized, cancellationToken);
    }

    public Task<Account?> GetByIdAsync(Guid accountId, CancellationToken cancellationToken)
    {
        return dbContext.Accounts.FirstOrDefaultAsync(x => x.Id == accountId, cancellationToken);
    }
}

internal sealed class EfDeviceRepository(ProductPlatformDbContext dbContext) : IDeviceRepository
{
    public Task AddAsync(Device device, CancellationToken cancellationToken)
    {
        return dbContext.Devices.AddAsync(device, cancellationToken).AsTask();
    }

    public Task<Device?> FindByFingerprintAsync(Guid accountId, string fingerprint, CancellationToken cancellationToken)
    {
        var normalized = fingerprint.Trim();
        return dbContext.Devices.FirstOrDefaultAsync(
            x => x.AccountId == accountId && x.Fingerprint == normalized,
            cancellationToken);
    }

    public Task<Device?> GetByIdAsync(Guid deviceId, CancellationToken cancellationToken)
    {
        return dbContext.Devices.FirstOrDefaultAsync(x => x.Id == deviceId, cancellationToken);
    }

    public async Task<IReadOnlyList<Device>> ListByAccountIdAsync(Guid accountId, CancellationToken cancellationToken)
    {
        return await dbContext.Devices
            .Where(x => x.AccountId == accountId)
            .OrderByDescending(x => x.LastSeenAtUtc)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountActiveByAccountIdAsync(Guid accountId, CancellationToken cancellationToken)
    {
        return dbContext.Devices.CountAsync(
            x => x.AccountId == accountId && x.Status == DeviceStatus.Active,
            cancellationToken);
    }
}

internal sealed class EfSubscriptionRepository(ProductPlatformDbContext dbContext) : ISubscriptionRepository
{
    public Task AddAsync(Subscription subscription, CancellationToken cancellationToken)
    {
        return dbContext.Subscriptions.AddAsync(subscription, cancellationToken).AsTask();
    }

    public Task<Subscription?> GetActiveByAccountIdAsync(Guid accountId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        return dbContext.Subscriptions
            .Include(x => x.Plan)
            .Where(x => x.AccountId == accountId)
            .OrderByDescending(x => x.EndsAtUtc)
            .FirstOrDefaultAsync(
                x => (x.Status == SubscriptionStatus.Active || x.Status == SubscriptionStatus.Trialing)
                    && x.StartsAtUtc <= now
                    && x.EndsAtUtc >= now,
                cancellationToken);
    }

    public Task<SubscriptionPlan?> GetDefaultPlanAsync(CancellationToken cancellationToken)
    {
        return dbContext.SubscriptionPlans
            .Where(x => x.IsActive)
            .OrderBy(x => x.PriceAmount)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task AddPlanAsync(SubscriptionPlan plan, CancellationToken cancellationToken)
    {
        return dbContext.SubscriptionPlans.AddAsync(plan, cancellationToken).AsTask();
    }
}

internal sealed class EfAccountSessionRepository(ProductPlatformDbContext dbContext) : IAccountSessionRepository
{
    public Task AddAsync(AccountSession session, CancellationToken cancellationToken)
    {
        return dbContext.AccountSessions.AddAsync(session, cancellationToken).AsTask();
    }

    public Task<AccountSession?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return dbContext.AccountSessions.FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken);
    }

    public async Task<IReadOnlyList<AccountSession>> ListByAccountIdAsync(Guid accountId, CancellationToken cancellationToken)
    {
        return await dbContext.AccountSessions
            .Where(x => x.AccountId == accountId)
            .OrderByDescending(x => x.LastSeenAtUtc)
            .ToListAsync(cancellationToken);
    }
}

internal sealed class EfAccessGrantRepository(ProductPlatformDbContext dbContext) : IAccessGrantRepository
{
    public Task AddAsync(AccessGrant accessGrant, CancellationToken cancellationToken)
    {
        return dbContext.AccessGrants.AddAsync(accessGrant, cancellationToken).AsTask();
    }

    public async Task<IReadOnlyList<AccessGrant>> ListByAccountIdAsync(Guid accountId, CancellationToken cancellationToken)
    {
        return await dbContext.AccessGrants
            .Include(x => x.Device)
            .Where(x => x.AccountId == accountId)
            .OrderByDescending(x => x.IssuedAtUtc)
            .ToListAsync(cancellationToken);
    }
}

internal sealed class EfUnitOfWork(ProductPlatformDbContext dbContext) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
