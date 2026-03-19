using System.Text.Json;

namespace VpnControlPlane.Application.Nodes;

public sealed record PeerMetadataSnapshot(
    string? PresharedKey,
    string? ClientPrivateKey,
    string? VpnUserExternalId,
    string? VpnDisplayName,
    string? VpnEmail,
    DateTimeOffset? IssuedAtUtc,
    string? ProductAccountId,
    string? ProductAccountEmail,
    string? ProductAccountDisplayName,
    string? ProductDeviceId,
    string? ProductDeviceName,
    string? ProductDevicePlatform,
    string? ProductDeviceFingerprint,
    string? ProductClientVersion)
{
    public static readonly PeerMetadataSnapshot Empty = new(
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null);
}

public static class PeerMetadataParser
{
    public static PeerMetadataSnapshot Parse(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return PeerMetadataSnapshot.Empty;
        }

        using var document = JsonDocument.Parse(metadataJson);

        string? presharedKey = null;
        string? clientPrivateKey = null;
        string? vpnUserExternalId = null;
        string? vpnDisplayName = null;
        string? vpnEmail = null;
        string? issuedAtRaw = null;
        string? productAccountId = null;
        string? productAccountEmail = null;
        string? productAccountDisplayName = null;
        string? productDeviceId = null;
        string? productDeviceName = null;
        string? productDevicePlatform = null;
        string? productDeviceFingerprint = null;
        string? productClientVersion = null;

        var root = document.RootElement;
        if (root.TryGetProperty("sources", out var sources) && sources.ValueKind == JsonValueKind.Array)
        {
            foreach (var source in sources.EnumerateArray())
            {
                ReadSource(
                    source,
                    ref presharedKey,
                    ref clientPrivateKey,
                    ref vpnUserExternalId,
                    ref vpnDisplayName,
                    ref vpnEmail,
                    ref issuedAtRaw,
                    ref productAccountId,
                    ref productAccountEmail,
                    ref productAccountDisplayName,
                    ref productDeviceId,
                    ref productDeviceName,
                    ref productDevicePlatform,
                    ref productDeviceFingerprint,
                    ref productClientVersion);
            }
        }
        else
        {
            ReadSource(
                root,
                ref presharedKey,
                ref clientPrivateKey,
                ref vpnUserExternalId,
                ref vpnDisplayName,
                ref vpnEmail,
                ref issuedAtRaw,
                ref productAccountId,
                ref productAccountEmail,
                ref productAccountDisplayName,
                ref productDeviceId,
                ref productDeviceName,
                ref productDevicePlatform,
                ref productDeviceFingerprint,
                ref productClientVersion);
        }

        DateTimeOffset? issuedAtUtc = DateTimeOffset.TryParse(issuedAtRaw, out var parsedIssuedAtUtc)
            ? parsedIssuedAtUtc
            : null;

        return new PeerMetadataSnapshot(
            presharedKey,
            clientPrivateKey,
            vpnUserExternalId,
            vpnDisplayName,
            vpnEmail,
            issuedAtUtc,
            productAccountId,
            productAccountEmail,
            productAccountDisplayName,
            productDeviceId,
            productDeviceName,
            productDevicePlatform,
            productDeviceFingerprint,
            productClientVersion);
    }

    private static void ReadSource(
        JsonElement source,
        ref string? presharedKey,
        ref string? clientPrivateKey,
        ref string? vpnUserExternalId,
        ref string? vpnDisplayName,
        ref string? vpnEmail,
        ref string? issuedAtRaw,
        ref string? productAccountId,
        ref string? productAccountEmail,
        ref string? productAccountDisplayName,
        ref string? productDeviceId,
        ref string? productDeviceName,
        ref string? productDevicePlatform,
        ref string? productDeviceFingerprint,
        ref string? productClientVersion)
    {
        if (source.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (source.TryGetProperty("peerProperties", out var peerProperties) && peerProperties.ValueKind == JsonValueKind.Object)
        {
            ApplyString(ref presharedKey, peerProperties, "PresharedKey");
        }

        if (source.TryGetProperty("metadata", out var metadata) && metadata.ValueKind == JsonValueKind.Object)
        {
            ApplyString(ref clientPrivateKey, metadata, "vpn-client-private-key");
            ApplyString(ref vpnUserExternalId, metadata, "vpn-user-id");
            ApplyString(ref vpnDisplayName, metadata, "vpn-display-name");
            ApplyString(ref vpnEmail, metadata, "vpn-email");
            ApplyString(ref issuedAtRaw, metadata, "vpn-issued-at");
            ApplyString(ref productAccountId, metadata, "product-account-id");
            ApplyString(ref productAccountEmail, metadata, "product-account-email");
            ApplyString(ref productAccountDisplayName, metadata, "product-account-display-name");
            ApplyString(ref productDeviceId, metadata, "product-device-id");
            ApplyString(ref productDeviceName, metadata, "product-device-name");
            ApplyString(ref productDevicePlatform, metadata, "product-device-platform");
            ApplyString(ref productDeviceFingerprint, metadata, "product-device-fingerprint");
            ApplyString(ref productClientVersion, metadata, "product-client-version");
        }

        if (source.TryGetProperty("userData", out var userData) && userData.ValueKind == JsonValueKind.Object)
        {
            ApplyString(ref vpnDisplayName, userData, "clientName");
        }
    }

    private static void ApplyString(ref string? target, JsonElement container, string propertyName)
    {
        if (target is not null
            || !container.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String)
        {
            return;
        }

        var value = property.GetString();
        if (!string.IsNullOrWhiteSpace(value))
        {
            target = value.Trim();
        }
    }
}
