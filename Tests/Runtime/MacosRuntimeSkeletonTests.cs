using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using VpnClient.Core.Interfaces;
using VpnClient.Core.Models;
using VpnClient.Infrastructure.Runtime;
using Xunit;

namespace VpnClient.Tests.Runtime;

public sealed class MacosRuntimeSkeletonTests
{
    [Fact]
    public async Task ConnectAsync_ReturnsUnsupportedOutsideMacOs()
    {
        var adapter = new MacosVpnRuntimeAdapter(
            new RecordingMacosTransport(),
            new RecordingKillSwitchService(),
            new FakeRuntimeEnvironment(isMacOS: false),
            NullLogger<MacosVpnRuntimeAdapter>.Instance);

        var state = await adapter.ConnectAsync(BuildProfile());

        Assert.Equal(RuntimeConnectionStatus.Unsupported, state.Status);
        Assert.Contains(state.Warnings, warning => warning.Contains("macOS runtime bridge", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Transport_IsUnavailableOutsideMacOs()
    {
        var transport = new UnixDomainSocketMacosRuntimeBridgeTransport("/tmp/etovpn-daemon.sock");

        Assert.False(await transport.IsAvailableAsync());
    }

    [Fact]
    public async Task ConnectAsync_UsesEnvelopeCommands_AndParsesStatusPayload()
    {
        var profile = BuildProfile();
        var transport = new RecordingMacosTransport
        {
            Available = true
        };

        transport.QueueResponse("""{"ok":true,"payload":{"helperVersion":"0.1.0"}}""");
        transport.QueueResponse("""{"ok":true,"payload":{"configured":true}}""");
        transport.QueueResponse("""{"ok":true,"payload":{"accepted":true}}""");
        transport.QueueResponse($$"""
            {
              "ok": true,
              "payload": {
                "state": "connected",
                "connected": true,
                "profileId": "{{profile.Id}}",
                "profileName": "{{profile.DisplayName}}",
                "serverEndpoint": "{{profile.Endpoint}}",
                "deviceIpv4Address": "10.8.1.2/32",
                "dns": ["1.1.1.1", "1.0.0.1"],
                "allowedIps": ["0.0.0.0/0", "::/0"],
                "rxBytes": 1024,
                "txBytes": 2048,
                "latestHandshakeAtUtc": "2026-03-20T12:34:56Z"
              }
            }
            """);

        var killSwitch = new RecordingKillSwitchService();
        var adapter = new MacosVpnRuntimeAdapter(
            transport,
            killSwitch,
            new FakeRuntimeEnvironment(isMacOS: true),
            NullLogger<MacosVpnRuntimeAdapter>.Instance);

        var state = await adapter.ConnectAsync(profile);

        Assert.Equal(RuntimeConnectionStatus.Connected, state.Status);
        Assert.Equal(profile.Id, state.ProfileId);
        Assert.Equal(profile.DisplayName, state.ProfileName);
        Assert.Equal(profile.Endpoint, state.Endpoint);
        Assert.Equal(1024, state.ReceivedBytes);
        Assert.Equal(2048, state.SentBytes);
        Assert.Equal(profile.Endpoint, killSwitch.ArmedEndpoint);

        Assert.Equal(4, transport.Requests.Count);
        Assert.Equal("hello", transport.Requests[0]["command"]?.GetValue<string>());
        Assert.Equal("configure", transport.Requests[1]["command"]?.GetValue<string>());
        Assert.Equal("activate", transport.Requests[2]["command"]?.GetValue<string>());
        Assert.Equal("status", transport.Requests[3]["command"]?.GetValue<string>());

        var configurePayload = transport.Requests[1]["payload"]!.AsObject();
        Assert.Equal(profile.Id.ToString("D"), configurePayload["profileId"]?.GetValue<string>());
        Assert.Equal(profile.DisplayName, configurePayload["profileName"]?.GetValue<string>());
        Assert.Equal(profile.RawSource, configurePayload["rawConfig"]?.GetValue<string>());

        var activatePayload = transport.Requests[2]["payload"]!.AsObject();
        Assert.Equal("client-private-key", activatePayload["privateKey"]?.GetValue<string>());
        Assert.NotNull(activatePayload["tunnelConfig"]);
    }

    [Fact]
    public async Task DisconnectAsync_SendsDeactivateEnvelope_AndDisarmsKillSwitch()
    {
        var profile = BuildProfile();
        var transport = new RecordingMacosTransport
        {
            Available = true
        };

        transport.QueueResponse("""{"ok":true}""");
        transport.QueueResponse("""{"ok":true}""");
        transport.QueueResponse("""{"ok":true}""");
        transport.QueueResponse("""{"ok":true,"payload":{"state":"connected","connected":true}}""");
        transport.QueueResponse("""{"ok":true,"payload":{"state":"disconnected","connected":false}}""");

        var killSwitch = new RecordingKillSwitchService();
        var adapter = new MacosVpnRuntimeAdapter(
            transport,
            killSwitch,
            new FakeRuntimeEnvironment(isMacOS: true),
            NullLogger<MacosVpnRuntimeAdapter>.Instance);

        await adapter.ConnectAsync(profile);
        var state = await adapter.DisconnectAsync();

        Assert.Equal(RuntimeConnectionStatus.Disconnected, state.Status);
        Assert.True(killSwitch.Disarmed);
        Assert.Equal("deactivate", transport.Requests.Last()["command"]?.GetValue<string>());
        Assert.Equal(profile.Id.ToString("D"), transport.Requests.Last()["payload"]?["profileId"]?.GetValue<string>());
    }

    private static ImportedServerProfile BuildProfile()
    {
        var interfaceValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        interfaceValues["Address"] = "10.8.1.2/32";
        interfaceValues["DNS"] = "1.1.1.1, 1.0.0.1";
        interfaceValues["MTU"] = "1280";
        interfaceValues["PrivateKey"] = "client-private-key";

        var peerValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        peerValues["PublicKey"] = "server-public-key";
        peerValues["AllowedIPs"] = "0.0.0.0/0, ::/0";
        peerValues["Endpoint"] = "45.136.49.191:443";

        var awgValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Jc"] = "3"
        };

        var config = new TunnelConfig(
            TunnelConfigFormat.WireGuardConf,
            "raw-config",
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
            "macos",
            new ImportedTunnelConfig(
                "macos",
                "macos.conf",
                "/tmp/macos.conf",
                TunnelConfigFormat.WireGuardConf,
                DateTimeOffset.UtcNow,
                "raw-config",
                """{"dns1":"1.1.1.1","dns2":"1.0.0.1"}""",
                config),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }

    private sealed class FakeRuntimeEnvironment : IRuntimeEnvironment
    {
        public FakeRuntimeEnvironment(bool isMacOS)
        {
            IsMacOS = isMacOS;
        }

        public bool IsWindows => false;

        public bool IsMacOS { get; }
    }

    private sealed class RecordingKillSwitchService : IKillSwitchService
    {
        public string? ArmedEndpoint { get; private set; }

        public bool Disarmed { get; private set; }

        public bool IsArmed => !Disarmed && !string.IsNullOrWhiteSpace(ArmedEndpoint);

        public Task ArmAsync(string endpoint, CancellationToken cancellationToken = default)
        {
            ArmedEndpoint = endpoint;
            Disarmed = false;
            return Task.CompletedTask;
        }

        public Task DisarmAsync(CancellationToken cancellationToken = default)
        {
            Disarmed = true;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingMacosTransport : IMacosRuntimeBridgeTransport
    {
        private readonly Queue<string> _responses = new();

        public bool Available { get; set; }

        public List<JsonObject> Requests { get; } = new();

        public void QueueResponse(string json)
        {
            _responses.Enqueue(json);
        }

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) => Task.FromResult(Available);

        public Task SendAsync(JsonObject payload, CancellationToken cancellationToken = default)
        {
            Requests.Add(Clone(payload));
            return Task.CompletedTask;
        }

        public Task<JsonDocument> RequestAsync(JsonObject payload, CancellationToken cancellationToken = default)
        {
            Requests.Add(Clone(payload));
            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No queued macOS bridge response is available for the test.");
            }

            return Task.FromResult(JsonDocument.Parse(_responses.Dequeue()));
        }

        private static JsonObject Clone(JsonObject payload)
        {
            return JsonNode.Parse(payload.ToJsonString())!.AsObject();
        }
    }
}
