namespace VpnProductPlatform.Contracts;

public sealed record SubscriptionSummaryResponse(
    Guid SubscriptionId,
    string PlanName,
    string Status,
    int MaxDevices,
    int MaxConcurrentSessions,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc);
