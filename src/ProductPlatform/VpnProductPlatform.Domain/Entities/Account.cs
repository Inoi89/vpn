using VpnProductPlatform.Domain.Common;
using VpnProductPlatform.Domain.Enums;

namespace VpnProductPlatform.Domain.Entities;

public sealed class Account : AuditableEntity
{
    private readonly List<Device> _devices = [];
    private readonly List<Subscription> _subscriptions = [];
    private readonly List<AccessGrant> _accessGrants = [];
    private readonly List<AccountSession> _sessions = [];

    private Account()
    {
    }

    private Account(
        Guid id,
        string email,
        string displayName,
        string passwordHash,
        AccountStatus status,
        DateTimeOffset now)
    {
        Id = id;
        Email = NormalizeEmail(email);
        DisplayName = NormalizeRequired(displayName, nameof(displayName));
        PasswordHash = NormalizeRequired(passwordHash, nameof(passwordHash));
        Status = status;
        MarkCreated(now);
    }

    public string Email { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public AccountStatus Status { get; private set; }

    public DateTimeOffset? LastLoginAtUtc { get; private set; }

    public IReadOnlyCollection<Device> Devices => _devices;

    public IReadOnlyCollection<Subscription> Subscriptions => _subscriptions;

    public IReadOnlyCollection<AccessGrant> AccessGrants => _accessGrants;

    public IReadOnlyCollection<AccountSession> Sessions => _sessions;

    public static Account Create(
        Guid id,
        string email,
        string displayName,
        string passwordHash,
        DateTimeOffset now)
    {
        return new Account(id, email, displayName, passwordHash, AccountStatus.PendingVerification, now);
    }

    public void RecordLogin(DateTimeOffset now)
    {
        LastLoginAtUtc = now;
        MarkUpdated(now);
    }

    public void UpdatePassword(string passwordHash, DateTimeOffset now)
    {
        PasswordHash = NormalizeRequired(passwordHash, nameof(passwordHash));
        MarkUpdated(now);
    }

    public void UpdateProfile(string displayName, DateTimeOffset now)
    {
        DisplayName = NormalizeRequired(displayName, nameof(displayName));
        MarkUpdated(now);
    }

    public void ChangeStatus(AccountStatus status, DateTimeOffset now)
    {
        Status = status;
        MarkUpdated(now);
    }

    public void VerifyEmail(DateTimeOffset now)
    {
        if (Status == AccountStatus.Active)
        {
            return;
        }

        Status = AccountStatus.Active;
        MarkUpdated(now);
    }

    private static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required.", nameof(email));
        }

        return email.Trim().ToLowerInvariant();
    }

    private static string NormalizeRequired(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{paramName} is required.", paramName);
        }

        return value.Trim();
    }
}
