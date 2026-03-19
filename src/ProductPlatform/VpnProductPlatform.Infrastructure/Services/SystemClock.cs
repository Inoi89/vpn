using VpnProductPlatform.Application.Abstractions;

namespace VpnProductPlatform.Infrastructure.Services;

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
