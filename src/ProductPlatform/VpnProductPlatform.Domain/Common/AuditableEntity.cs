namespace VpnProductPlatform.Domain.Common;

public abstract class AuditableEntity
{
    public Guid Id { get; protected set; }

    public DateTimeOffset CreatedAtUtc { get; protected set; }

    public DateTimeOffset? UpdatedAtUtc { get; protected set; }

    protected void MarkCreated(DateTimeOffset now)
    {
        CreatedAtUtc = now;
        UpdatedAtUtc = now;
    }

    protected void MarkUpdated(DateTimeOffset now)
    {
        UpdatedAtUtc = now;
    }
}
