using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VpnControlPlane.Domain.Entities;
using VpnControlPlane.Domain.Enums;

namespace VpnControlPlane.Infrastructure.Persistence.Configurations;

internal sealed class NodeConfiguration : IEntityTypeConfiguration<Node>
{
    public void Configure(EntityTypeBuilder<Node> builder)
    {
        builder.ToTable("nodes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AgentIdentifier).HasMaxLength(128);
        builder.Property(x => x.Name).HasMaxLength(200);
        builder.Property(x => x.Cluster).HasMaxLength(128);
        builder.Property(x => x.AgentBaseAddress).HasMaxLength(512);
        builder.Property(x => x.CertificateThumbprint).HasMaxLength(128);
        builder.Property(x => x.Description).HasMaxLength(1024);
        builder.Property(x => x.AgentVersion).HasMaxLength(64);
        builder.Property(x => x.LastError).HasMaxLength(2048);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(x => x.AgentIdentifier).IsUnique();
        builder.Navigation(x => x.PeerConfigs).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(x => x.Sessions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class VpnUserConfiguration : IEntityTypeConfiguration<VpnUser>
{
    public void Configure(EntityTypeBuilder<VpnUser> builder)
    {
        builder.ToTable("users");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalId).HasMaxLength(128);
        builder.Property(x => x.DisplayName).HasMaxLength(200);
        builder.Property(x => x.Email).HasMaxLength(256);
        builder.HasIndex(x => x.ExternalId).IsUnique();
        builder.HasIndex(x => x.Email).IsUnique();
        builder.Navigation(x => x.PeerConfigs).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(x => x.Sessions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class PeerConfigConfiguration : IEntityTypeConfiguration<PeerConfig>
{
    public void Configure(EntityTypeBuilder<PeerConfig> builder)
    {
        builder.ToTable("peer_configs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DisplayName).HasMaxLength(200);
        builder.Property(x => x.PublicKey).HasMaxLength(128);
        builder.Property(x => x.AllowedIps).HasMaxLength(1024);
        builder.Property(x => x.ProtocolFlavor).HasConversion<string>().HasMaxLength(64);
        builder.Property(x => x.MetadataJson).HasColumnType("jsonb");
        builder.HasIndex(x => new { x.NodeId, x.PublicKey }).IsUnique();
        builder.HasOne(x => x.Node).WithMany(x => x.PeerConfigs).HasForeignKey(x => x.NodeId);
        builder.HasOne(x => x.User).WithMany(x => x.PeerConfigs).HasForeignKey(x => x.UserId);
    }
}

internal sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("sessions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PeerPublicKey).HasMaxLength(128);
        builder.Property(x => x.Endpoint).HasMaxLength(256);
        builder.Property(x => x.State).HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(x => new { x.NodeId, x.PeerPublicKey }).IsUnique();
        builder.HasOne(x => x.Node).WithMany(x => x.Sessions).HasForeignKey(x => x.NodeId);
        builder.HasOne(x => x.User).WithMany(x => x.Sessions).HasForeignKey(x => x.UserId);
        builder.HasOne(x => x.PeerConfig).WithMany().HasForeignKey(x => x.PeerConfigId);
    }
}

internal sealed class TrafficStatsConfiguration : IEntityTypeConfiguration<TrafficStats>
{
    public void Configure(EntityTypeBuilder<TrafficStats> builder)
    {
        builder.ToTable("traffic_stats");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.NodeId, x.UserId, x.CapturedAtUtc });
        builder.HasOne(x => x.Node).WithMany().HasForeignKey(x => x.NodeId);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        builder.HasOne(x => x.Session).WithMany().HasForeignKey(x => x.SessionId);
        builder.HasOne(x => x.PeerConfig).WithMany().HasForeignKey(x => x.PeerConfigId);
    }
}
