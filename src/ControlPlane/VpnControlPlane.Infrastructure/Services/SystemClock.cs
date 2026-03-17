using VpnControlPlane.Application.Abstractions;

namespace VpnControlPlane.Infrastructure.Services;

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
