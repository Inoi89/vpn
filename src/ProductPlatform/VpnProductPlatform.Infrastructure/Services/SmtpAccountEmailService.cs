using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using VpnProductPlatform.Application.Abstractions;
using VpnProductPlatform.Infrastructure.Security;

namespace VpnProductPlatform.Infrastructure.Services;

public sealed class SmtpAccountEmailService(
    IOptions<SmtpOptions> smtpOptions,
    IOptions<EmailVerificationOptions> emailVerificationOptions,
    ILogger<SmtpAccountEmailService> logger) : IAccountEmailService
{
    public async Task SendVerificationAsync(
        string email,
        string displayName,
        string verificationToken,
        CancellationToken cancellationToken)
    {
        var options = smtpOptions.Value;
        if (!options.Enabled || string.IsNullOrWhiteSpace(options.Host))
        {
            return;
        }

        try
        {
            var verificationUrl = BuildVerificationUrl(emailVerificationOptions.Value.CabinetBaseUrl, verificationToken);
            var message = BuildVerificationMessage(options, email, displayName, verificationUrl);

            using var client = new SmtpClient();
            await client.ConnectAsync(
                options.Host,
                options.Port,
                ParseSecureSocketOptions(options.SecureSocketMode),
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(options.UserName))
            {
                await client.AuthenticateAsync(
                    options.UserName,
                    options.Password ?? string.Empty,
                    cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to send verification email to {Email}.", email);
        }
    }

    private static MimeMessage BuildVerificationMessage(
        SmtpOptions options,
        string email,
        string displayName,
        string verificationUrl)
    {
        var plainText = $"""
            Привет, {displayName}!

            Спасибо за регистрацию в EtoJeSim VPN.
            Чтобы активировать аккаунт, подтвердите почту по ссылке:
            {verificationUrl}

            Если письмо получили не вы, просто проигнорируйте его.
            """;

        var html = $"""
            <html>
              <body style="font-family: Segoe UI, Arial, sans-serif; color: #102033;">
                <h2 style="margin-bottom: 12px;">Привет, {Escape(displayName)}!</h2>
                <p>Спасибо за регистрацию в <strong>EtoJeSim VPN</strong>.</p>
                <p>Чтобы активировать аккаунт, подтвердите почту по ссылке ниже.</p>
                <p><a href="{Escape(verificationUrl)}">{Escape(verificationUrl)}</a></p>
                <p style="color:#5d6b7c">Если письмо получили не вы, просто проигнорируйте его.</p>
              </body>
            </html>
            """;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(options.FromName, options.FromEmail));
        message.To.Add(new MailboxAddress(displayName, email));
        message.Subject = "Подтвердите почту для EtoJeSim VPN";
        message.Body = new BodyBuilder
        {
            TextBody = plainText,
            HtmlBody = html
        }.ToMessageBody();

        return message;
    }

    private static SecureSocketOptions ParseSecureSocketOptions(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "none" => SecureSocketOptions.None,
            "auto" => SecureSocketOptions.Auto,
            "ssl" or "sslonconnect" => SecureSocketOptions.SslOnConnect,
            "starttlswhennavailable" => SecureSocketOptions.StartTlsWhenAvailable,
            _ => SecureSocketOptions.StartTls
        };
    }

    private static string Escape(string value)
    {
        return System.Net.WebUtility.HtmlEncode(value);
    }

    private static string BuildVerificationUrl(string baseUrl, string verificationToken)
    {
        var trimmedBaseUrl = string.IsNullOrWhiteSpace(baseUrl)
            ? "https://etovpn.com"
            : baseUrl.Trim().TrimEnd('/');
        var encodedToken = Uri.EscapeDataString(verificationToken);
        return $"{trimmedBaseUrl}/?verify={encodedToken}";
    }
}
