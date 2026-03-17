using VpnControlPlane.Domain.Common;
using VpnControlPlane.Domain.Enums;

namespace VpnControlPlane.Domain.Entities;

public sealed class Node : AuditableEntity
{
    private readonly List<PeerConfig> _peerConfigs = [];
    private readonly List<Session> _sessions = [];

    private Node()
    {
    }

    private Node(
        Guid id,
        string agentIdentifier,
        string name,
        string cluster,
        string agentBaseAddress,
        string? certificateThumbprint,
        string? description,
        DateTimeOffset now)
    {
        Id = id;
        AgentIdentifier = agentIdentifier;
        Name = name;
        Cluster = cluster;
        AgentBaseAddress = NormalizeAddress(agentBaseAddress);
        CertificateThumbprint = certificateThumbprint;
        Description = description;
        Status = NodeStatus.Provisioning;
        IsEnabled = true;
        MarkCreated(now);
    }

    public string AgentIdentifier { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string Cluster { get; private set; } = string.Empty;

    public string AgentBaseAddress { get; private set; } = string.Empty;

    public string? CertificateThumbprint { get; private set; }

    public string? Description { get; private set; }

    public NodeStatus Status { get; private set; }

    public string? AgentVersion { get; private set; }

    public DateTimeOffset? LastSeenAtUtc { get; private set; }

    public string? LastError { get; private set; }

    public bool IsEnabled { get; private set; }

    public IReadOnlyCollection<PeerConfig> PeerConfigs => _peerConfigs;

    public IReadOnlyCollection<Session> Sessions => _sessions;

    public static Node Register(
        Guid id,
        string agentIdentifier,
        string name,
        string cluster,
        string agentBaseAddress,
        string? certificateThumbprint,
        string? description,
        DateTimeOffset now)
    {
        ValidateRequired(agentIdentifier, nameof(agentIdentifier));
        ValidateRequired(name, nameof(name));
        ValidateRequired(cluster, nameof(cluster));
        ValidateRequired(agentBaseAddress, nameof(agentBaseAddress));

        return new Node(
            id,
            agentIdentifier.Trim(),
            name.Trim(),
            cluster.Trim(),
            agentBaseAddress.Trim(),
            certificateThumbprint?.Trim(),
            description?.Trim(),
            now);
    }

    public void UpdateRegistration(
        string name,
        string cluster,
        string agentBaseAddress,
        string? certificateThumbprint,
        string? description,
        DateTimeOffset now)
    {
        ValidateRequired(name, nameof(name));
        ValidateRequired(cluster, nameof(cluster));
        ValidateRequired(agentBaseAddress, nameof(agentBaseAddress));

        Name = name.Trim();
        Cluster = cluster.Trim();
        AgentBaseAddress = NormalizeAddress(agentBaseAddress.Trim());
        CertificateThumbprint = string.IsNullOrWhiteSpace(certificateThumbprint)
            ? null
            : certificateThumbprint.Trim();
        Description = string.IsNullOrWhiteSpace(description)
            ? null
            : description.Trim();
        Status = IsEnabled ? NodeStatus.Provisioning : NodeStatus.Disabled;
        LastError = null;
        MarkUpdated(now);
    }

    public void MarkHealthy(string? agentVersion, DateTimeOffset observedAtUtc)
    {
        AgentVersion = string.IsNullOrWhiteSpace(agentVersion) ? AgentVersion : agentVersion.Trim();
        LastSeenAtUtc = observedAtUtc;
        LastError = null;
        Status = IsEnabled ? NodeStatus.Healthy : NodeStatus.Disabled;
        MarkUpdated(observedAtUtc);
    }

    public void MarkUnreachable(string error, DateTimeOffset observedAtUtc)
    {
        LastSeenAtUtc = observedAtUtc;
        LastError = string.IsNullOrWhiteSpace(error) ? "Unknown agent polling failure." : error.Trim();
        Status = IsEnabled ? NodeStatus.Unreachable : NodeStatus.Disabled;
        MarkUpdated(observedAtUtc);
    }

    public void Disable(DateTimeOffset now)
    {
        IsEnabled = false;
        Status = NodeStatus.Disabled;
        MarkUpdated(now);
    }

    private static void ValidateRequired(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", paramName);
        }
    }

    private static string NormalizeAddress(string value)
    {
        return value.TrimEnd('/');
    }
}
