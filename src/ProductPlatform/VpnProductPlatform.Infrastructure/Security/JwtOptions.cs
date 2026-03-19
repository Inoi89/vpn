namespace VpnProductPlatform.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "VpnProductPlatform";

    public string Audience { get; set; } = "VpnProductPlatform.Client";

    public string SigningKey { get; set; } = "development-signing-key-change-me-please";

    public int LifetimeMinutes { get; set; } = 1440;

    public int RefreshLifetimeDays { get; set; } = 30;
}
