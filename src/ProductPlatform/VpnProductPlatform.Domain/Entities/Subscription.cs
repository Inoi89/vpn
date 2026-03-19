using VpnProductPlatform.Domain.Common;
using VpnProductPlatform.Domain.Enums;

namespace VpnProductPlatform.Domain.Entities;

public sealed class Subscription : AuditableEntity
{
    private Subscription()
    {
    }

    private Subscription(
        Guid id,
        Guid accountId,
        Guid planId,
        SubscriptionStatus status,
        DateTimeOffset startsAtUtc,
        DateTimeOffset endsAtUtc,
        DateTimeOffset? graceEndsAtUtc,
        DateTimeOffset now)
    {
        Id = id;
        AccountId = accountId;
        PlanId = planId;
        Status = status;
        StartsAtUtc = startsAtUtc;
        EndsAtUtc = endsAtUtc;
        GraceEndsAtUtc = graceEndsAtUtc;
        MarkCreated(now);
    }

    public Guid AccountId { get; private set; }

    public Account Account { get; private set; } = null!;

    public Guid PlanId { get; private set; }

    public SubscriptionPlan Plan { get; private set; } = null!;

    public SubscriptionStatus Status { get; private set; }

    public DateTimeOffset StartsAtUtc { get; private set; }

    public DateTimeOffset EndsAtUtc { get; private set; }

    public DateTimeOffset? GraceEndsAtUtc { get; private set; }

    public static Subscription CreateTrial(
        Guid id,
        Guid accountId,
        Guid planId,
        DateTimeOffset now,
        TimeSpan duration)
    {
        return new Subscription(
            id,
            accountId,
            planId,
            SubscriptionStatus.Trialing,
            now,
            now.Add(duration),
            graceEndsAtUtc: null,
            now);
    }

    public bool IsActiveAt(DateTimeOffset now)
    {
        return Status is SubscriptionStatus.Active or SubscriptionStatus.Trialing
            && StartsAtUtc <= now
            && EndsAtUtc >= now;
    }

    public void Activate(DateTimeOffset now)
    {
        Status = SubscriptionStatus.Active;
        MarkUpdated(now);
    }

    public void Expire(DateTimeOffset now)
    {
        Status = SubscriptionStatus.Expired;
        MarkUpdated(now);
    }
}
