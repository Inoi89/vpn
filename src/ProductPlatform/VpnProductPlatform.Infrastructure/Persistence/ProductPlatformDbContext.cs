using Microsoft.EntityFrameworkCore;
using VpnProductPlatform.Domain.Entities;

namespace VpnProductPlatform.Infrastructure.Persistence;

public sealed class ProductPlatformDbContext(DbContextOptions<ProductPlatformDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<Device> Devices => Set<Device>();

    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();

    public DbSet<Subscription> Subscriptions => Set<Subscription>();

    public DbSet<AccountSession> AccountSessions => Set<AccountSession>();

    public DbSet<AccessGrant> AccessGrants => Set<AccessGrant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(builder =>
        {
            builder.ToTable("accounts");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => x.Email).IsUnique();
            builder.Property(x => x.Email).HasMaxLength(320).IsRequired();
            builder.Property(x => x.DisplayName).HasMaxLength(120).IsRequired();
            builder.Property(x => x.PasswordHash).IsRequired();
            builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            builder.HasMany(x => x.Devices).WithOne(x => x.Account).HasForeignKey(x => x.AccountId);
            builder.HasMany(x => x.Subscriptions).WithOne(x => x.Account).HasForeignKey(x => x.AccountId);
            builder.HasMany(x => x.AccessGrants).WithOne(x => x.Account).HasForeignKey(x => x.AccountId);
            builder.HasMany(x => x.Sessions).WithOne(x => x.Account).HasForeignKey(x => x.AccountId);
        });

        modelBuilder.Entity<Device>(builder =>
        {
            builder.ToTable("devices");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => new { x.AccountId, x.Fingerprint }).IsUnique();
            builder.Property(x => x.DeviceName).HasMaxLength(120).IsRequired();
            builder.Property(x => x.Platform).HasMaxLength(64).IsRequired();
            builder.Property(x => x.Fingerprint).HasMaxLength(256).IsRequired();
            builder.Property(x => x.ClientVersion).HasMaxLength(32);
            builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        });

        modelBuilder.Entity<SubscriptionPlan>(builder =>
        {
            builder.ToTable("subscription_plans");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => x.Name).IsUnique();
            builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
            builder.Property(x => x.Currency).HasMaxLength(8).IsRequired();
            builder.Property(x => x.PriceAmount).HasColumnType("numeric(12,2)");
            builder.HasMany(x => x.Subscriptions).WithOne(x => x.Plan).HasForeignKey(x => x.PlanId);
        });

        modelBuilder.Entity<Subscription>(builder =>
        {
            builder.ToTable("subscriptions");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => new { x.AccountId, x.Status });
            builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        });

        modelBuilder.Entity<AccountSession>(builder =>
        {
            builder.ToTable("account_sessions");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => x.AccountId);
            builder.Property(x => x.RefreshTokenHash).HasMaxLength(128).IsRequired();
            builder.Property(x => x.IpAddress).HasMaxLength(64);
            builder.Property(x => x.UserAgent).HasMaxLength(512);
            builder.Property(x => x.RevokedReason).HasMaxLength(256);
        });

        modelBuilder.Entity<AccessGrant>(builder =>
        {
            builder.ToTable("access_grants");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => new { x.AccountId, x.DeviceId });
            builder.Property(x => x.PeerPublicKey).HasMaxLength(128);
            builder.Property(x => x.ConfigFormat).HasMaxLength(32).IsRequired();
            builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            builder.HasOne(x => x.Device).WithMany().HasForeignKey(x => x.DeviceId);
        });
    }
}
