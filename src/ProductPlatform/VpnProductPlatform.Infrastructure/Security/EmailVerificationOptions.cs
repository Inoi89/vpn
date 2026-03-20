namespace VpnProductPlatform.Infrastructure.Security;

public sealed class EmailVerificationOptions
{
    public const string SectionName = "EmailVerification";

    public string Issuer { get; set; } = "VpnProductPlatform";
    public string Audience { get; set; } = "VpnProductPlatform.EmailVerification";
    public string SigningKey { get; set; } = string.Empty;
    public int LifetimeHours { get; set; } = 24;
    public string CabinetBaseUrl { get; set; } = "https://etovpn.com";
}
