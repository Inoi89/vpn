using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using VpnClient.Core.Interfaces;
using VpnClient.Core.Models;
using VpnClient.Core.Models.Auth;

namespace VpnClient.Infrastructure.Auth;

public sealed class ProductPlatformEnrollmentService : IProductPlatformEnrollmentService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly IProductPlatformAuthService _authService;
    private readonly ILocalDeviceIdentityService _localDeviceIdentityService;
    private readonly IImportService _importService;

    public ProductPlatformEnrollmentService(
        ProductPlatformOptions options,
        IProductPlatformAuthService authService,
        ILocalDeviceIdentityService localDeviceIdentityService,
        IImportService importService)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = BuildBaseUri(options.ApiBaseUrl)
        };
        _authService = authService;
        _localDeviceIdentityService = localDeviceIdentityService;
        _importService = importService;
    }

    public async Task<ImportedServerProfile> EnsureManagedProfileAsync(CancellationToken cancellationToken = default)
    {
        var session = await _authService.GetCurrentSessionAsync(cancellationToken)
            ?? throw new InvalidOperationException("Войдите в аккаунт, чтобы получить управляемый доступ.");

        var identity = await _localDeviceIdentityService.GetOrCreateAsync(cancellationToken);
        var device = await SendAsync<DeviceResponse>(
            session.AccessToken,
            HttpMethod.Post,
            "devices",
            new RegisterDeviceRequest(
                identity.DeviceName,
                identity.Platform,
                identity.Fingerprint,
                identity.ClientVersion),
            cancellationToken);

        var nodes = await SendAsync<IReadOnlyList<IssuableNodeResponse>>(
            session.AccessToken,
            HttpMethod.Get,
            "access-grants/nodes",
            payload: null,
            cancellationToken);

        var node = nodes
            .OrderBy(x => x.ActiveSessions)
            .ThenBy(x => x.EnabledPeerCount)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("Сейчас нет доступных VPN-нод для выдачи.");

        var issued = await SendAsync<IssuedAccessGrantResponse>(
            session.AccessToken,
            HttpMethod.Post,
            "access-grants",
            new IssueAccessGrantRequest(device.DeviceId, node.NodeId, "amnezia-vpn"),
            cancellationToken);

        var imported = await _importService.ImportFromContentAsync(
            issued.ClientConfigFileName,
            issued.ClientConfig,
            $"managed://{issued.AccessGrantId}",
            cancellationToken);

        return new ImportedServerProfile(
            Guid.NewGuid(),
            node.Name,
            imported with
            {
                DisplayName = node.Name
            },
            imported.ImportedAtUtc,
            imported.ImportedAtUtc,
            new ManagedProfileBinding(
                session.AccountId,
                session.Email,
                device.DeviceId,
                issued.AccessGrantId,
                issued.NodeId,
                issued.ControlPlaneAccessId,
                issued.ConfigFormat));
    }

    private async Task<T> SendAsync<T>(
        string accessToken,
        HttpMethod method,
        string relativePath,
        object? payload,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, relativePath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload);
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var model = await response.Content.ReadFromJsonAsync<T>(SerializerOptions, cancellationToken);
            return model ?? throw new InvalidOperationException("Product platform returned an empty response.");
        }

        var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException(TryExtractError(errorBody)
            ?? $"Product platform request failed with status {(int)response.StatusCode}.");
    }

    private static Uri BuildBaseUri(string apiBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(apiBaseUrl))
        {
            throw new InvalidOperationException("ProductPlatform:ApiBaseUrl is not configured.");
        }

        return new Uri(apiBaseUrl.Trim().TrimEnd('/') + "/", UriKind.Absolute);
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

    private sealed record RegisterDeviceRequest(
        string DeviceName,
        string Platform,
        string Fingerprint,
        string? ClientVersion);

    private sealed record DeviceResponse(
        Guid DeviceId,
        string DeviceName,
        string Platform,
        string? ClientVersion,
        string Fingerprint,
        string Status,
        DateTimeOffset LastSeenAtUtc);

    private sealed record IssuableNodeResponse(
        Guid NodeId,
        string Name,
        string Status,
        int ActiveSessions,
        int EnabledPeerCount);

    private sealed record IssueAccessGrantRequest(
        Guid DeviceId,
        Guid NodeId,
        string? ConfigFormat);

    private sealed record IssuedAccessGrantResponse(
        Guid AccessGrantId,
        Guid DeviceId,
        string DeviceName,
        Guid NodeId,
        Guid? ControlPlaneAccessId,
        string? PeerPublicKey,
        string? AllowedIps,
        string ConfigFormat,
        string Status,
        DateTimeOffset IssuedAtUtc,
        DateTimeOffset? ExpiresAtUtc,
        DateTimeOffset? RevokedAtUtc,
        string ClientConfigFileName,
        string ClientConfig);
}
