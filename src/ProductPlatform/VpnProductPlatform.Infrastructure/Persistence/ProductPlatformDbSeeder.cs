using Microsoft.EntityFrameworkCore;
using VpnProductPlatform.Domain.Entities;

namespace VpnProductPlatform.Infrastructure.Persistence;

public sealed class ProductPlatformDbSeeder(ProductPlatformDbContext dbContext)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            alter table if exists access_grants
                add column if not exists control_plane_access_id uuid;
            alter table if exists access_grants
                add column if not exists allowed_ips character varying(128);
            create index if not exists ix_access_grants_control_plane_access_id
                on access_grants (control_plane_access_id);
            """,
            cancellationToken);

        var hasPlan = await dbContext.SubscriptionPlans.AnyAsync(cancellationToken);
        if (hasPlan)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var plan = SubscriptionPlan.Create(
            Guid.NewGuid(),
            "MVP Trial",
            maxDevices: 2,
            maxConcurrentSessions: 1,
            priceAmount: 0m,
            currency: "USD",
            billingPeriodMonths: 1,
            isActive: true,
            now);

        await dbContext.SubscriptionPlans.AddAsync(plan, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
