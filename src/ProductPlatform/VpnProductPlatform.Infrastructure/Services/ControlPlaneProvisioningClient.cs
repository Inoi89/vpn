using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using VpnProductPlatform.Application.Abstractions;

namespace VpnProductPlatform.Infrastructure.Services;

internal sealed class ControlPlaneProvisioningClient(
    HttpClient httpClient,
    IOptions<ControlPlaneOptions> options) : IControlPlaneProvisioningClient
{
    private readonly ControlPlaneOptions _options = options.Value;

    public async Task<IReadOnlyList<ControlPlaneNodeEnvelope>> ListNodesAsync(CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, "/api/nodes");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(await BuildErrorMessageAsync("list control plane nodes", response, cancellationToken));
        }

        var payload = await response.Content.ReadFromJsonAsync<IReadOnlyList<ControlPlaneNodeDto>>(cancellationToken: cancellationToken) ?? [];
        return payload
            .Select(x => new ControlPlaneNodeEnvelope(x.Id, x.Name, x.Status, x.ActiveSessions, x.EnabledPeerCount))
            .ToArray();
    }

    public async Task<ControlPlaneIssuedAccessEnvelope> IssueAccessAsync(ControlPlaneIssueAccessRequest requestPayload, CancellationToken cancellationToken)
    {
        var payload = new ControlPlaneIssueNodeAccessRequest(
            requestPayload.DisplayName,
            requestPayload.Email,
            requestPayload.ConfigFormat,
            new ControlPlaneProductMetadataDto(
                requestPayload.ProductMetadata.AccountId.ToString("D"),
                requestPayload.ProductMetadata.AccountEmail,
                requestPayload.ProductMetadata.AccountDisplayName,
                requestPayload.ProductMetadata.DeviceId.ToString("D"),
                requestPayload.ProductMetadata.DeviceName,
                requestPayload.ProductMetadata.DevicePlatform,
                requestPayload.ProductMetadata.DeviceFingerprint,
                requestPayload.ProductMetadata.ClientVersion));

        using var request = CreateRequest(HttpMethod.Post, $"/api/nodes/{requestPayload.NodeId:D}/accesses");
        request.Content = JsonContent.Create(payload);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(await BuildErrorMessageAsync("issue control plane access", response, cancellationToken));
        }

        var payloadResponse = await response.Content.ReadFromJsonAsync<ControlPlaneIssuedAccessDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Control plane returned an empty access issue response.");

        return new ControlPlaneIssuedAccessEnvelope(
            payloadResponse.NodeId,
            payloadResponse.AccessId,
            payloadResponse.UserId,
            payloadResponse.ExternalId,
            payloadResponse.DisplayName,
            payloadResponse.Email,
            payloadResponse.PublicKey,
            payloadResponse.AllowedIps,
            payloadResponse.ClientConfigFileName,
            payloadResponse.ClientConfig);
    }

    public async Task<ControlPlaneAccessConfigEnvelope> GetAccessConfigAsync(Guid nodeId, Guid accessId, string configFormat, CancellationToken cancellationToken)
    {
        var encodedFormat = Uri.EscapeDataString(configFormat);
        using var request = CreateRequest(HttpMethod.Get, $"/api/nodes/{nodeId:D}/accesses/{accessId:D}/config?format={encodedFormat}");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(await BuildErrorMessageAsync("load control plane access config", response, cancellationToken));
        }

        var payloadResponse = await response.Content.ReadFromJsonAsync<ControlPlaneAccessConfigDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Control plane returned an empty access config response.");

        return new ControlPlaneAccessConfigEnvelope(
            payloadResponse.NodeId,
            payloadResponse.AccessId,
            configFormat,
            payloadResponse.ClientConfigFileName,
            payloadResponse.ClientConfig);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            throw new InvalidOperationException("ControlPlane:BaseUrl is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_options.JwtSigningKey))
        {
            throw new InvalidOperationException("ControlPlane:JwtSigningKey is not configured.");
        }

        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", IssueAccessToken());
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private string IssueAccessToken()
    {
        var now = DateTimeOffset.UtcNow;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.JwtSigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, _options.JwtSubject),
            new(ClaimTypes.Role, _options.JwtRole)
        };

        var token = new JwtSecurityToken(
            issuer: _options.JwtIssuer,
            audience: _options.JwtAudience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: now.AddMinutes(10).UtcDateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static async Task<string> BuildErrorMessageAsync(string operation, HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var fallback = $"Failed to {operation}. HTTP {(int)response.StatusCode}.";
        try
        {
            var payload = await response.Content.ReadFromJsonAsync<ErrorPayload>(cancellationToken: cancellationToken);
            return payload?.Error ?? payload?.Title ?? payload?.Detail ?? fallback;
        }
        catch
        {
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            return string.IsNullOrWhiteSpace(text) ? fallback : text.Trim();
        }
    }

    private sealed record ControlPlaneNodeDto(
        Guid Id,
        string Name,
        string Status,
        int ActiveSessions,
        int EnabledPeerCount);

    private sealed record ControlPlaneIssueNodeAccessRequest(
        string DisplayName,
        string? Email,
        string ConfigFormat,
        ControlPlaneProductMetadataDto ProductMetadata);

    private sealed record ControlPlaneProductMetadataDto(
        string AccountId,
        string AccountEmail,
        string AccountDisplayName,
        string DeviceId,
        string DeviceName,
        string DevicePlatform,
        string DeviceFingerprint,
        string? ClientVersion);

    private sealed record ControlPlaneIssuedAccessDto(
        Guid NodeId,
        Guid? AccessId,
        Guid UserId,
        string ExternalId,
        string DisplayName,
        string? Email,
        string PublicKey,
        string AllowedIps,
        string ClientConfigFileName,
        string ClientConfig);

    private sealed record ControlPlaneAccessConfigDto(
        Guid NodeId,
        Guid AccessId,
        Guid UserId,
        string PublicKey,
        string ClientConfigFileName,
        string ClientConfig);

    private sealed record ErrorPayload(string? Error, string? Title, string? Detail);
}
