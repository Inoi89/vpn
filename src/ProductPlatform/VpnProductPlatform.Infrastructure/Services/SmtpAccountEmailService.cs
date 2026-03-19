using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using VpnProductPlatform.Application.Abstractions;

namespace VpnProductPlatform.Infrastructure.Services;

public sealed class SmtpAccountEmailService(
    IOptions<SmtpOptions> smtpOptions,
    ILogger<SmtpAccountEmailService> logger) : IAccountEmailService
{
    public async Task SendWelcomeAsync(
        string email,
        string displayName,
        string? planName,
        DateTimeOffset? subscriptionEndsAtUtc,
        CancellationToken cancellationToken)
    {
        var options = smtpOptions.Value;
        if (!options.Enabled || string.IsNullOrWhiteSpace(options.Host))
        {
            return;
        }

        try
        {
            var message = BuildWelcomeMessage(options, email, displayName, planName, subscriptionEndsAtUtc);

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
            logger.LogWarning(exception, "Failed to send welcome email to {Email}.", email);
        }
    }

    private static MimeMessage BuildWelcomeMessage(
        SmtpOptions options,
        string email,
        string displayName,
        string? planName,
        DateTimeOffset? subscriptionEndsAtUtc)
    {
        var effectivePlanName = string.IsNullOrWhiteSpace(planName) ? "ваш тариф" : planName;
        var expiresLine = subscriptionEndsAtUtc is null
            ? "Доступ уже создан и готов к использованию."
            : $"Текущий доступ активен до {subscriptionEndsAtUtc.Value:dd.MM.yyyy HH:mm} UTC.";

        var plainText = $"""
            Привет, {displayName}!

            Спасибо за регистрацию в EtoJeSim VPN.
            Для аккаунта уже активирован план "{effectivePlanName}".
            {expiresLine}

            Если письмо получили не вы, просто проигнорируйте его.
            """;

        var html = $"""
            <html>
              <body style="font-family: Segoe UI, Arial, sans-serif; color: #102033;">
                <h2 style="margin-bottom: 12px;">Привет, {Escape(displayName)}!</h2>
                <p>Спасибо за регистрацию в <strong>EtoJeSim VPN</strong>.</p>
                <p>Для аккаунта уже активирован план <strong>{Escape(effectivePlanName)}</strong>.</p>
                <p>{Escape(expiresLine)}</p>
                <p style="color:#5d6b7c">Если письмо получили не вы, просто проигнорируйте его.</p>
              </body>
            </html>
            """;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(options.FromName, options.FromEmail));
        message.To.Add(new MailboxAddress(displayName, email));
        message.Subject = "Спасибо за регистрацию в EtoJeSim VPN";
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
}
