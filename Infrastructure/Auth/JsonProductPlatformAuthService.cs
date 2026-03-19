using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using VpnClient.Core.Interfaces;
using VpnClient.Core.Models.Auth;

namespace VpnClient.Infrastructure.Auth;

public sealed class JsonProductPlatformAuthService : IProductPlatformAuthService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly HttpClient _httpClient;
    private readonly ProductPlatformOptions _options;
    private readonly string _filePath;

    public JsonProductPlatformAuthService(ProductPlatformOptions options)
    {
        _options = options;
        _httpClient = new HttpClient
        {
            BaseAddress = BuildBaseUri(options.ApiBaseUrl)
        };
        _filePath = GetDefaultStoragePath();
        EnsureDirectory();
    }

    public static string GetDefaultStoragePath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "YourVpnClient",
            "account-session.json");
    }

    public async Task<ProductPlatformSession?> GetCurrentSessionAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var session = await ReadSessionAsync(cancellationToken);
            if (session is null)
            {
                return null;
            }

            var now = DateTimeOffset.UtcNow;
            if (!session.IsAccessTokenExpired(now))
            {
                return session;
            }

            if (session.IsRefreshTokenExpired(now))
            {
                await ClearSessionAsync(cancellationToken);
                return null;
            }

            var refreshed = await RefreshAsync(session, cancellationToken);
            await WriteSessionAsync(refreshed, cancellationToken);
            return refreshed;
        }
        catch
        {
            await ClearSessionAsync(cancellationToken);
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ProductPlatformSession> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException("Email is required.");
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Password is required.");
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var payload = await SendAsync<AuthTokenResponse>(
                HttpMethod.Post,
                "auth/login",
                new LoginRequest(email.Trim(), password),
                bearerToken: null,
                cancellationToken);

            var session = ToSession(payload);
            await WriteSessionAsync(session, cancellationToken);
            return session;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var session = await ReadSessionAsync(cancellationToken);
            if (session is not null)
            {
                try
                {
                    await SendAsync<object>(
                        HttpMethod.Post,
                        "auth/logout",
                        payload: null,
                        bearerToken: session.AccessToken,
                        cancellationToken);
                }
                catch
                {
                    // Best-effort remote logout.
                }
            }

            await ClearSessionAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<ProductPlatformSession> RefreshAsync(ProductPlatformSession session, CancellationToken cancellationToken)
    {
        var payload = await SendAsync<AuthTokenResponse>(
            HttpMethod.Post,
            "auth/refresh",
            new RefreshTokenRequest(session.RefreshToken),
            bearerToken: null,
            cancellationToken);

        return ToSession(payload);
    }

    private async Task<T> SendAsync<T>(
        HttpMethod method,
        string relativePath,
        object? payload,
        string? bearerToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, relativePath);
        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload);
        }

        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            if (typeof(T) == typeof(object))
            {
                return default!;
            }

            var model = await response.Content.ReadFromJsonAsync<T>(SerializerOptions, cancellationToken);
            return model ?? throw new InvalidOperationException("The product platform returned an empty response.");
        }

        var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var error = TryExtractError(errorBody);
        throw new InvalidOperationException(string.IsNullOrWhiteSpace(error)
            ? $"Product platform request failed with status {(int)response.StatusCode}."
            : error);
    }

    private async Task<ProductPlatformSession?> ReadSessionAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(_filePath, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<ProductPlatformSession>(json, SerializerOptions);
    }

    private async Task WriteSessionAsync(ProductPlatformSession session, CancellationToken cancellationToken)
    {
        EnsureDirectory();
        var json = JsonSerializer.Serialize(session, SerializerOptions);
        await File.WriteAllTextAsync(_filePath, json, cancellationToken);
    }

    private Task ClearSessionAsync(CancellationToken cancellationToken)
    {
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }

        return Task.CompletedTask;
    }

    private void EnsureDirectory()
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static ProductPlatformSession ToSession(AuthTokenResponse response)
    {
        return new ProductPlatformSession(
            response.AccountId,
            response.Email,
            response.DisplayName,
            response.SessionId,
            response.AccessToken,
            response.ExpiresAtUtc,
            response.RefreshToken,
            response.RefreshTokenExpiresAtUtc);
    }

    private static Uri BuildBaseUri(string apiBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(apiBaseUrl))
        {
            throw new InvalidOperationException("ProductPlatform:ApiBaseUrl is not configured.");
        }

        var normalized = apiBaseUrl.Trim().TrimEnd('/') + "/";
        return new Uri(normalized, UriKind.Absolute);
    }

    private static string? TryExtractError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("error", out var error)
                && error.ValueKind == JsonValueKind.String)
            {
                return error.GetString();
            }
        }
        catch
        {
            // Ignore malformed payloads.
        }

        return body.Trim();
    }

    private sealed record LoginRequest(string Email, string Password);

    private sealed record RefreshTokenRequest(string RefreshToken);

    private sealed record AuthTokenResponse(
        Guid AccountId,
        string Email,
        string DisplayName,
        Guid SessionId,
        string AccessToken,
        DateTimeOffset ExpiresAtUtc,
        string RefreshToken,
        DateTimeOffset RefreshTokenExpiresAtUtc);
}
