using Microsoft.Extensions.DependencyInjection;
using VpnProductPlatform.Application.Accounts;
using VpnProductPlatform.Application.AccessGrants;
using VpnProductPlatform.Application.Devices;

namespace VpnProductPlatform.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddProductPlatformApplication(this IServiceCollection services)
    {
        services.AddScoped<AccountApplicationService>();
        services.AddScoped<AccessGrantApplicationService>();
        services.AddScoped<SessionApplicationService>();
        services.AddScoped<DeviceApplicationService>();
        return services;
    }
}
