using VpnProductPlatform.Domain.Common;

namespace VpnProductPlatform.Domain.Entities;

public sealed class SubscriptionPlan : AuditableEntity
{
    private readonly List<Subscription> _subscriptions = [];

    private SubscriptionPlan()
    {
    }

    private SubscriptionPlan(
        Guid id,
        string name,
        int maxDevices,
        int maxConcurrentSessions,
        decimal priceAmount,
        string currency,
        int billingPeriodMonths,
        bool isActive,
        DateTimeOffset now)
    {
        Id = id;
        Name = NormalizeRequired(name, nameof(name));
        MaxDevices = maxDevices;
        MaxConcurrentSessions = maxConcurrentSessions;
        PriceAmount = priceAmount;
        Currency = NormalizeRequired(currency, nameof(currency)).ToUpperInvariant();
        BillingPeriodMonths = billingPeriodMonths;
        IsActive = isActive;
        MarkCreated(now);
    }

    public string Name { get; private set; } = string.Empty;

    public int MaxDevices { get; private set; }

    public int MaxConcurrentSessions { get; private set; }

    public decimal PriceAmount { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public int BillingPeriodMonths { get; private set; }

    public bool IsActive { get; private set; }

    public IReadOnlyCollection<Subscription> Subscriptions => _subscriptions;

    public static SubscriptionPlan Create(
        Guid id,
        string name,
        int maxDevices,
        int maxConcurrentSessions,
        decimal priceAmount,
        string currency,
        int billingPeriodMonths,
        bool isActive,
        DateTimeOffset now)
    {
        if (maxDevices < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDevices));
        }

        if (maxConcurrentSessions < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxConcurrentSessions));
        }

        if (billingPeriodMonths < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(billingPeriodMonths));
        }

        return new SubscriptionPlan(
            id,
            name,
            maxDevices,
            maxConcurrentSessions,
            priceAmount,
            currency,
            billingPeriodMonths,
            isActive,
            now);
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
