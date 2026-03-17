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
    }
}
