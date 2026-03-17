using Microsoft.EntityFrameworkCore;
using VpnControlPlane.Application.Abstractions;
using VpnControlPlane.Domain.Entities;

namespace VpnControlPlane.Infrastructure.Persistence;

public sealed class ControlPlaneDbContext(DbContextOptions<ControlPlaneDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<Node> Nodes => Set<Node>();

    public DbSet<VpnUser> Users => Set<VpnUser>();

    public DbSet<PeerConfig> PeerConfigs => Set<PeerConfig>();

    public DbSet<Session> Sessions => Set<Session>();

    public DbSet<TrafficStats> TrafficStats => Set<TrafficStats>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ControlPlaneDbContext).Assembly);
        ApplySnakeCaseColumnNames(modelBuilder);
    }

    private static void ApplySnakeCaseColumnNames(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));
            }
        }
    }

    private static string ToSnakeCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var buffer = new System.Text.StringBuilder(value.Length + 8);
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (char.IsUpper(character) && index > 0)
            {
                buffer.Append('_');
            }

            buffer.Append(char.ToLowerInvariant(character));
        }

        return buffer.ToString();
    }
}
