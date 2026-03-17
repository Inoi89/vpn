namespace VpnControlPlane.Domain.Common;

public abstract class AuditableEntity
{
    public Guid Id { get; protected init; }

    public DateTimeOffset CreatedAtUtc { get; protected set; }

    public DateTimeOffset UpdatedAtUtc { get; protected set; }

    protected void MarkCreated(DateTimeOffset timestamp)
    {
        CreatedAtUtc = timestamp;
        UpdatedAtUtc = timestamp;
    }

    protected void MarkUpdated(DateTimeOffset timestamp)
    {
        UpdatedAtUtc = timestamp;
    }
}
