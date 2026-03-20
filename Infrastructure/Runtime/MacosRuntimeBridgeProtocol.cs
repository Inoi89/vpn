using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using VpnClient.Core.Models;

namespace VpnClient.Infrastructure.Runtime;

internal static class MacosRuntimeBridgeProtocol
{
    public const string DefaultSocketFilename = "etoVPN.runtime.sock";

    public static string DefaultSocketPath => Path.Combine(Path.GetTempPath(), DefaultSocketFilename);

    public static class Commands
    {
        public const string Hello = "hello";
        public const string Health = "health";
        public const string Configure = "configure";
        public const string Activate = "activate";
        public const string Deactivate = "deactivate";
        public const string Status = "status";
        public const string Logs = "logs";
        public const string Quit = "quit";
    }

    public static JsonObject BuildHelloRequest()
    {
        var version = typeof(MacosRuntimeBridgeProtocol).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        return BuildRequest(
            Commands.Hello,
            new JsonObject
            {
                ["client"] = "etoVPN.Desktop",
                ["clientVersion"] = version,
                ["platform"] = "macos"
            });
    }

    public static JsonObject BuildHealthRequest() => BuildRequest(Commands.Health);

    public static JsonObject BuildConfigureRequest(ImportedServerProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return BuildRequest(Commands.Configure, BuildProfilePayload(profile));
    }

    public static JsonObject BuildActivateRequest(ImportedServerProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return BuildRequest(Commands.Activate, BuildProfilePayload(profile));
    }

    public static JsonObject BuildDeactivateRequest(Guid? profileId = null)
    {
        var payload = new JsonObject();
        if (profileId is not null)
        {
            payload["profileId"] = profileId.Value.ToString("D");
        }

        return BuildRequest(Commands.Deactivate, payload);
    }

    public static JsonObject BuildStatusRequest() => BuildRequest(Commands.Status);

    public static JsonObject BuildLogsRequest(int? limit = null)
    {
        var payload = new JsonObject();
        if (limit is not null)
        {
            payload["limit"] = limit.Value;
        }

        return BuildRequest(Commands.Logs, payload);
    }

    public static JsonObject BuildQuitRequest() => BuildRequest(Commands.Quit);

    public static void EnsureSuccess(JsonDocument response)
    {
        ArgumentNullException.ThrowIfNull(response);

        var root = response.RootElement;
        if (!root.TryGetProperty("ok", out var okElement))
        {
            return;
        }

        if (okElement.ValueKind == JsonValueKind.True)
        {
            return;
        }

        throw new InvalidOperationException(TryExtractError(root) ?? "macOS runtime bridge returned an error response.");
    }

    public static JsonElement ExtractPayloadOrRoot(JsonDocument response)
    {
        ArgumentNullException.ThrowIfNull(response);
        EnsureSuccess(response);

        var root = response.RootElement;
        return root.TryGetProperty("payload", out var payload)
            ? payload.Clone()
            : root.Clone();
    }

    public static string? TryExtractError(JsonElement root)
    {
        if (root.TryGetProperty("error", out var errorElement))
        {
            if (errorElement.ValueKind == JsonValueKind.String)
            {
                return errorElement.GetString();
            }

            if (errorElement.ValueKind == JsonValueKind.Object)
            {
                if (errorElement.TryGetProperty("message", out var messageElement)
                    && messageElement.ValueKind == JsonValueKind.String)
                {
                    return messageElement.GetString();
                }

                if (errorElement.TryGetProperty("code", out var codeElement)
                    && codeElement.ValueKind == JsonValueKind.String)
                {
                    return codeElement.GetString();
                }
            }
        }

        if (root.TryGetProperty("message", out var rootMessage)
            && rootMessage.ValueKind == JsonValueKind.String)
        {
            return rootMessage.GetString();
        }

        return null;
    }

    private static JsonObject BuildRequest(string command, JsonObject? payload = null)
    {
        return new JsonObject
        {
            ["id"] = Guid.NewGuid().ToString("D"),
            ["type"] = "request",
            ["command"] = command,
            ["payload"] = payload ?? new JsonObject()
        };
    }

    private static JsonObject BuildProfilePayload(ImportedServerProfile profile)
    {
        var config = profile.TunnelConfig;
        var payload = new JsonObject
        {
            ["profileId"] = profile.Id.ToString("D"),
            ["profileName"] = profile.DisplayName,
            ["sourceFormat"] = profile.SourceFormat.ToString(),
            ["sourceFileName"] = profile.SourceFileName,
            ["endpoint"] = profile.Endpoint,
            ["address"] = profile.Address,
            ["dns"] = BuildArray(profile.DnsServers),
            ["mtu"] = ParseNullableInt(profile.Mtu) is { } mtu ? JsonValue.Create(mtu) : null,
            ["allowedIps"] = BuildArray(profile.AllowedIps),
            ["publicKey"] = profile.PublicKey,
            ["presharedKey"] = profile.PresharedKey,
            ["rawConfig"] = config.RawConfig,
            ["rawPackageJson"] = profile.RawPackageJson,
            ["tunnelConfig"] = new JsonObject
            {
                ["format"] = config.Format.ToString(),
                ["address"] = config.Address,
                ["dns"] = BuildArray(config.DnsServers),
                ["mtu"] = ParseNullableInt(config.Mtu) is { } configMtu ? JsonValue.Create(configMtu) : null,
                ["allowedIps"] = BuildArray(config.AllowedIps),
                ["endpoint"] = config.Endpoint,
                ["publicKey"] = config.PublicKey,
                ["presharedKey"] = config.PresharedKey,
                ["persistentKeepalive"] = config.PersistentKeepalive is { } keepalive ? JsonValue.Create(keepalive) : null,
                ["interfaceValues"] = BuildDictionary(config.InterfaceValues),
                ["peerValues"] = BuildDictionary(config.PeerValues),
                ["awgValues"] = BuildDictionary(config.AwgValues)
            }
        };

        if (config.InterfaceValues.TryGetValue("PrivateKey", out var privateKey)
            && !string.IsNullOrWhiteSpace(privateKey))
        {
            payload["privateKey"] = privateKey;
        }

        if (profile.ManagedProfile is not null)
        {
            payload["managedProfile"] = new JsonObject
            {
                ["accountId"] = profile.ManagedProfile.AccountId.ToString("D"),
                ["accountEmail"] = profile.ManagedProfile.AccountEmail,
                ["deviceId"] = profile.ManagedProfile.DeviceId.ToString("D"),
                ["accessGrantId"] = profile.ManagedProfile.AccessGrantId.ToString("D"),
                ["nodeId"] = profile.ManagedProfile.NodeId.ToString("D"),
                ["controlPlaneAccessId"] = profile.ManagedProfile.ControlPlaneAccessId?.ToString("D"),
                ["configFormat"] = profile.ManagedProfile.ConfigFormat
            };
        }

        return payload;
    }

    private static JsonArray BuildArray(IEnumerable<string> values)
    {
        return new JsonArray(values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => JsonValue.Create(value))
            .ToArray());
    }

    private static JsonObject BuildDictionary(IReadOnlyDictionary<string, string> values)
    {
        var json = new JsonObject();
        foreach (var pair in values)
        {
            json[pair.Key] = pair.Value;
        }

        return json;
    }

    private static int? ParseNullableInt(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }
}
