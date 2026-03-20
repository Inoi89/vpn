using System.Text.Json;
using VpnClient.Core.Models;
using VpnClient.Infrastructure.Runtime;
using Xunit;

namespace VpnClient.Tests.Runtime;

public sealed class MacosRuntimeBridgeProtocolTests
{
    [Fact]
    public void BuildHelloRequest_UsesEnvelopeAndHelloCommand()
    {
        var request = MacosRuntimeBridgeProtocol.BuildHelloRequest();

        Assert.Equal("request", request["type"]?.GetValue<string>());
        Assert.Equal(MacosRuntimeBridgeProtocol.Commands.Hello, request["command"]?.GetValue<string>());
        Assert.False(string.IsNullOrWhiteSpace(request["id"]?.GetValue<string>()));
        Assert.Equal("etoVPN.Desktop", request["payload"]?["client"]?.GetValue<string>());
        Assert.Equal("macos", request["payload"]?["platform"]?.GetValue<string>());
    }

    [Fact]
    public void BuildConfigureRequest_IncludesProfileAndManagedPayload()
    {
        var profile = BuildProfile();

        var request = MacosRuntimeBridgeProtocol.BuildConfigureRequest(profile);
        var payload = request["payload"]!.AsObject();
        var tunnelConfig = payload["tunnelConfig"]!.AsObject();
        var managed = payload["managedProfile"]!.AsObject();

        Assert.Equal(MacosRuntimeBridgeProtocol.Commands.Configure, request["command"]?.GetValue<string>());
        Assert.Equal(profile.Id.ToString("D"), payload["profileId"]?.GetValue<string>());
        Assert.Equal(profile.DisplayName, payload["profileName"]?.GetValue<string>());
        Assert.Equal(profile.SourceFormat.ToString(), payload["sourceFormat"]?.GetValue<string>());
        Assert.Equal(profile.SourceFileName, payload["sourceFileName"]?.GetValue<string>());
        Assert.Equal(profile.Endpoint, payload["endpoint"]?.GetValue<string>());
        Assert.Equal(profile.RawPackageJson, payload["rawPackageJson"]?.GetValue<string>());
        Assert.Equal("client-private-key", payload["privateKey"]?.GetValue<string>());
        Assert.Equal(profile.TunnelConfig.Format.ToString(), tunnelConfig["format"]?.GetValue<string>());
        Assert.Equal("3", tunnelConfig["awgValues"]?["Jc"]?.GetValue<string>());
        Assert.Equal(profile.ManagedProfile!.AccessGrantId.ToString("D"), managed["accessGrantId"]?.GetValue<string>());
    }

    [Fact]
    public void ExtractPayloadOrRoot_ReturnsPayloadForSuccessEnvelope()
    {
        using var document = JsonDocument.Parse("""
            {
              "id": "1",
              "type": "response",
              "ok": true,
              "payload": {
                "state": "connected",
                "connected": true
              }
            }
            """);

        var payload = MacosRuntimeBridgeProtocol.ExtractPayloadOrRoot(document);

        Assert.True(payload.GetProperty("connected").GetBoolean());
        Assert.Equal("connected", payload.GetProperty("state").GetString());
    }

    [Fact]
    public void EnsureSuccess_ThrowsWithBridgeErrorMessage()
    {
        using var document = JsonDocument.Parse("""
            {
              "id": "1",
              "type": "response",
              "ok": false,
              "error": {
                "code": "not_implemented",
                "message": "Packet tunnel activation is not implemented.",
                "details": null
              }
            }
            """);

        var exception = Assert.Throws<InvalidOperationException>(() => MacosRuntimeBridgeProtocol.EnsureSuccess(document));

        Assert.Contains("Packet tunnel activation is not implemented.", exception.Message);
    }

    [Fact]
    public void DefaultSocketPath_UsesStableFilename()
    {
        Assert.EndsWith(MacosRuntimeBridgeProtocol.DefaultSocketFilename, MacosRuntimeBridgeProtocol.DefaultSocketPath, StringComparison.Ordinal);
    }

    private static ImportedServerProfile BuildProfile()
    {
        var interfaceValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Address"] = "10.8.1.2/32",
            ["DNS"] = "1.1.1.1, 1.0.0.1",
            ["MTU"] = "1280",
            ["PrivateKey"] = "client-private-key"
        };

        var peerValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["PublicKey"] = "server-public-key",
            ["AllowedIPs"] = "0.0.0.0/0, ::/0",
            ["Endpoint"] = "45.136.49.191:443"
        };

        var awgValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Jc"] = "3"
        };

        var tunnelConfig = new TunnelConfig(
            TunnelConfigFormat.AmneziaVpn,
            "raw-wireguard-config",
            Array.Empty<ConfigLine>(),
            interfaceValues,
            peerValues,
            awgValues,
            "10.8.1.2/32",
            new[] { "1.1.1.1", "1.0.0.1" },
            "1280",
            new[] { "0.0.0.0/0", "::/0" },
            null,
            "45.136.49.191:443",
            "server-public-key",
            null);

        return new ImportedServerProfile(
            Guid.NewGuid(),
            "Frankfurt",
            new ImportedTunnelConfig(
                "Frankfurt",
                "frankfurt.vpn",
                "managed://frankfurt",
                TunnelConfigFormat.AmneziaVpn,
                DateTimeOffset.UtcNow,
                "vpn://encoded-source",
                """{"dns1":"1.1.1.1","dns2":"1.0.0.1"}""",
                tunnelConfig),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            new ManagedProfileBinding(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                "user@example.com",
                Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Guid.Parse("44444444-4444-4444-4444-444444444444"),
                Guid.Parse("55555555-5555-5555-5555-555555555555"),
                "amnezia-vpn"));
    }
}
