using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VpnProductPlatform.Application;
using VpnProductPlatform.Application.Abstractions;
using VpnProductPlatform.Infrastructure.Persistence;
using VpnProductPlatform.Infrastructure.Security;
using VpnProductPlatform.Infrastructure.Services;

namespace VpnProductPlatform.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddProductPlatformInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddProductPlatformApplication();

        var jwtSection = configuration.GetSection(JwtOptions.SectionName);
        var jwtOptions = new JwtOptions
        {
            Issuer = jwtSection["Issuer"] ?? "VpnProductPlatform",
            Audience = jwtSection["Audience"] ?? "VpnProductPlatform.Client",
            SigningKey = jwtSection["SigningKey"] ?? "development-signing-key-change-me-please",
            LifetimeMinutes = int.TryParse(jwtSection["LifetimeMinutes"], out var lifetimeMinutes) ? lifetimeMinutes : 1440
        };
        services.AddSingleton<IOptions<JwtOptions>>(Options.Create(jwtOptions));

        services.AddDbContext<ProductPlatformDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("ProductPlatform")));

        services.AddScoped<IAccountRepository, EfAccountRepository>();
        services.AddScoped<IDeviceRepository, EfDeviceRepository>();
        services.AddScoped<ISubscriptionRepository, EfSubscriptionRepository>();
        services.AddScoped<IAccessGrantRepository, EfAccessGrantRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IClock, SystemClock>();
        services.AddScoped<IPasswordHashService, PasswordHashService>();
        services.AddScoped<ITokenIssuer, JwtTokenIssuer>();
        services.AddScoped<ProductPlatformDbSeeder>();

        return services;
    }
}
