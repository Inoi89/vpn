namespace VpnProductPlatform.Infrastructure.Services;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public bool Enabled { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string SecureSocketMode { get; set; } = "StartTls";
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string FromEmail { get; set; } = "no-reply@etojesim.com";
    public string FromName { get; set; } = "EtoJeSim VPN";
}
