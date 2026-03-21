using System.Net;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace VpnClient.Infrastructure.Auth;

internal static class ProductPlatformHttpClientFactory
{
    public static HttpClient Create(string apiBaseUrl)
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
            ConnectTimeout = TimeSpan.FromSeconds(15),
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            SslOptions =
            {
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck
            }
        };

        return new HttpClient(handler)
        {
            BaseAddress = BuildBaseUri(apiBaseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public static void PrepareJsonRequest(HttpRequestMessage request)
    {
        request.Version = HttpVersion.Version11;
        request.VersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public static string FormatTransportError(HttpRequestException exception)
    {
        var inner = exception.InnerException;
        if (inner is AuthenticationException)
        {
            return string.IsNullOrWhiteSpace(inner.Message)
                ? "SSL/TLS connection could not be established."
                : $"SSL/TLS connection could not be established: {inner.Message}";
        }

        if (inner is not null && !string.IsNullOrWhiteSpace(inner.Message))
        {
            return $"{exception.Message} ({inner.Message})";
        }

        return exception.Message;
    }

    public static Uri BuildBaseUri(string apiBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(apiBaseUrl))
        {
            throw new InvalidOperationException("ProductPlatform:ApiBaseUrl is not configured.");
        }

        return new Uri(apiBaseUrl.Trim().TrimEnd('/') + "/", UriKind.Absolute);
    }
}
