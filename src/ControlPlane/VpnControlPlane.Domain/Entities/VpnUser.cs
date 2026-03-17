using VpnControlPlane.Domain.Common;

namespace VpnControlPlane.Domain.Entities;

public sealed class VpnUser : AuditableEntity
{
    private readonly List<PeerConfig> _peerConfigs = [];
    private readonly List<Session> _sessions = [];

    private VpnUser()
    {
    }

    private VpnUser(
        Guid id,
        string externalId,
        string displayName,
        string? email,
        bool isEnabled,
        DateTimeOffset now)
    {
        Id = id;
        ExternalId = externalId;
        DisplayName = displayName;
        Email = email;
        IsEnabled = isEnabled;
        MarkCreated(now);
    }

    public string ExternalId { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public string? Email { get; private set; }

    public bool IsEnabled { get; private set; }

    public IReadOnlyCollection<PeerConfig> PeerConfigs => _peerConfigs;

    public IReadOnlyCollection<Session> Sessions => _sessions;

    public static VpnUser Create(
        Guid id,
        string externalId,
        string displayName,
        string? email,
        bool isEnabled,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(externalId))
        {
            throw new ArgumentException("External ID is required.", nameof(externalId));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name is required.", nameof(displayName));
        }

        return new VpnUser(
            id,
            externalId.Trim(),
            displayName.Trim(),
            string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
            isEnabled,
            now);
    }

    public void UpdateProfile(string displayName, string? email, bool isEnabled, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name is required.", nameof(displayName));
        }

        DisplayName = displayName.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        IsEnabled = isEnabled;
        MarkUpdated(now);
    }
}
