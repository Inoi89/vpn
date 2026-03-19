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
        var smtpSection = configuration.GetSection(SmtpOptions.SectionName);
        var smtpOptions = new SmtpOptions
        {
            Enabled = bool.TryParse(smtpSection["Enabled"], out var smtpEnabled) && smtpEnabled,
            Host = smtpSection["Host"] ?? string.Empty,
            Port = int.TryParse(smtpSection["Port"], out var smtpPort) ? smtpPort : 587,
            SecureSocketMode = smtpSection["SecureSocketMode"] ?? "StartTls",
            UserName = smtpSection["UserName"],
            Password = smtpSection["Password"],
            FromEmail = smtpSection["FromEmail"] ?? "no-reply@etojesim.com",
            FromName = smtpSection["FromName"] ?? "EtoJeSim VPN"
        };
        services.AddSingleton<IOptions<SmtpOptions>>(Options.Create(smtpOptions));

        var jwtSection = configuration.GetSection(JwtOptions.SectionName);
        var jwtOptions = new JwtOptions
        {
            Issuer = jwtSection["Issuer"] ?? "VpnProductPlatform",
            Audience = jwtSection["Audience"] ?? "VpnProductPlatform.Client",
            SigningKey = jwtSection["SigningKey"] ?? "development-signing-key-change-me-please",
            LifetimeMinutes = int.TryParse(jwtSection["LifetimeMinutes"], out var lifetimeMinutes) ? lifetimeMinutes : 1440,
            RefreshLifetimeDays = int.TryParse(jwtSection["RefreshLifetimeDays"], out var refreshLifetimeDays) ? refreshLifetimeDays : 30
        };
        services.AddSingleton<IOptions<JwtOptions>>(Options.Create(jwtOptions));

        services.AddDbContext<ProductPlatformDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("ProductPlatform")));

        services.AddScoped<IAccountRepository, EfAccountRepository>();
        services.AddScoped<IDeviceRepository, EfDeviceRepository>();
        services.AddScoped<ISubscriptionRepository, EfSubscriptionRepository>();
        services.AddScoped<IAccountSessionRepository, EfAccountSessionRepository>();
        services.AddScoped<IAccessGrantRepository, EfAccessGrantRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IClock, SystemClock>();
        services.AddScoped<IPasswordHashService, PasswordHashService>();
        services.AddScoped<ITokenIssuer, JwtTokenIssuer>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<IAccountEmailService, SmtpAccountEmailService>();
        services.AddScoped<ProductPlatformDbSeeder>();

        return services;
    }
}
