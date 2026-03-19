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

        var emailVerificationSection = configuration.GetSection(EmailVerificationOptions.SectionName);
        var emailVerificationOptions = new EmailVerificationOptions
        {
            Issuer = emailVerificationSection["Issuer"] ?? "VpnProductPlatform",
            Audience = emailVerificationSection["Audience"] ?? "VpnProductPlatform.EmailVerification",
            SigningKey = string.IsNullOrWhiteSpace(emailVerificationSection["SigningKey"]) ? jwtOptions.SigningKey : emailVerificationSection["SigningKey"]!,
            LifetimeHours = int.TryParse(emailVerificationSection["LifetimeHours"], out var verificationLifetimeHours) ? verificationLifetimeHours : 24,
            CabinetBaseUrl = emailVerificationSection["CabinetBaseUrl"] ?? "http://5.61.37.29"
        };
        services.AddSingleton<IOptions<EmailVerificationOptions>>(Options.Create(emailVerificationOptions));

        var controlPlaneSection = configuration.GetSection(ControlPlaneOptions.SectionName);
        var controlPlaneOptions = new ControlPlaneOptions
        {
            BaseUrl = controlPlaneSection["BaseUrl"] ?? string.Empty,
            JwtSigningKey = controlPlaneSection["JwtSigningKey"] ?? string.Empty,
            JwtIssuer = controlPlaneSection["JwtIssuer"] ?? "vpn-control-plane",
            JwtAudience = controlPlaneSection["JwtAudience"] ?? "vpn-control-plane-ui",
            JwtSubject = controlPlaneSection["JwtSubject"] ?? "product-platform",
            JwtRole = controlPlaneSection["JwtRole"] ?? "internal"
        };
        services.AddSingleton<IOptions<ControlPlaneOptions>>(Options.Create(controlPlaneOptions));

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
        services.AddScoped<IEmailVerificationTokenService, EmailVerificationTokenService>();
        services.AddScoped<IAccountEmailService, SmtpAccountEmailService>();
        services.AddHttpClient<IControlPlaneProvisioningClient, ControlPlaneProvisioningClient>((provider, client) =>
        {
            var opts = provider.GetRequiredService<IOptions<ControlPlaneOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(opts.BaseUrl))
            {
                client.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/'));
            }
        });
        services.AddScoped<ProductPlatformDbSeeder>();

        return services;
    }
}
