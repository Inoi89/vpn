using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using VpnClient.Core.Models;
using VpnClient.Infrastructure.Runtime;
using Xunit;

namespace VpnClient.Tests.Runtime;

public sealed class AmneziaDaemonRuntimeAdapterTests
{
    [Fact]
    public async Task ConnectAsync_BuildsDaemonPayloadWithoutDroppingAwgFields()
    {
        var transport = new RecordingDaemonTransport
        {
            Available = true,
            NextResponse = JsonDocument.Parse("""
                {
                  "type": "status",
                  "connected": true,
                  "serverIpv4Gateway": "45.136.49.191",
                  "deviceIpv4Address": "10.8.1.2/32",
                  "date": "Tue Mar 18 22:10:00 2026",
                  "txBytes": 128,
                  "rxBytes": 512
                }
                """)
        };

        var adapter = new AmneziaDaemonRuntimeAdapter(
            transport,
            new FakeRuntimeEnvironment(),
            NullLogger<AmneziaDaemonRuntimeAdapter>.Instance);

        var state = await adapter.ConnectAsync(BuildProfile());

        Assert.Equal(RuntimeConnectionStatus.Connected, state.Status);
        Assert.Contains(transport.SentPayloads, payload => payload["type"]!.GetValue<string>() == "activate");
        var activatePayload = transport.SentPayloads.First(payload => payload["type"]!.GetValue<string>() == "activate");
        Assert.Equal("10.8.1.2/32", activatePayload["deviceIpv4Address"]!.GetValue<string>());
        Assert.Equal("45.136.49.191", activatePayload["serverIpv4Gateway"]!.GetValue<string>());
        Assert.Equal("1.1.1.1", activatePayload["primaryDnsServer"]!.GetValue<string>());
        Assert.Equal("1.0.0.1", activatePayload["secondaryDnsServer"]!.GetValue<string>());
        Assert.Equal("2", activatePayload["Jc"]!.GetValue<string>());
        Assert.Equal("283091219", activatePayload["H4"]!.GetValue<string>());

        var ranges = activatePayload["allowedIPAddressRanges"]!.AsArray();
        Assert.Equal(2, ranges.Count);
        Assert.Equal("0.0.0.0", ranges[0]!["address"]!.GetValue<string>());
        Assert.Equal(0, ranges[0]!["range"]!.GetValue<int>());
        Assert.False(ranges[0]!["isIpv6"]!.GetValue<bool>());
        Assert.Equal("::", ranges[1]!["address"]!.GetValue<string>());
        Assert.True(ranges[1]!["isIpv6"]!.GetValue<bool>());
    }

    private static ImportedServerProfile BuildProfile()
    {
        var interfaceValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Address"] = "10.8.1.2/32",
            ["DNS"] = "1.1.1.1, 1.0.0.1",
            ["MTU"] = "1280",
            ["PrivateKey"] = "client-private-key",
            ["Jc"] = "2",
            ["Jmin"] = "10",
            ["Jmax"] = "50",
            ["S1"] = "94",
            ["S2"] = "146",
            ["H1"] = "2097057167",
            ["H2"] = "2385741147",
            ["H3"] = "3630987908",
            ["H4"] = "283091219"
        };

        var awgValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Jc"] = "2",
            ["Jmin"] = "10",
            ["Jmax"] = "50",
            ["S1"] = "94",
            ["S2"] = "146",
            ["H1"] = "2097057167",
            ["H2"] = "2385741147",
            ["H3"] = "3630987908",
            ["H4"] = "283091219"
        };

        var peerValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["PublicKey"] = "server-public-key",
            ["PresharedKey"] = "server-psk",
            ["AllowedIPs"] = "0.0.0.0/0, ::/0",
            ["Endpoint"] = "45.136.49.191:45393",
            ["PersistentKeepalive"] = "25"
        };

        var config = new TunnelConfig(
            TunnelConfigFormat.AmneziaAwgNative,
            "raw",
            [],
            interfaceValues,
            peerValues,
            awgValues,
            "10.8.1.2/32",
            ["1.1.1.1", "1.0.0.1"],
            "1280",
            ["0.0.0.0/0", "::/0"],
            25,
            "45.136.49.191:45393",
            "server-public-key",
            "server-psk");

        return new ImportedServerProfile(
            Guid.NewGuid(),
            "Probe",
            new ImportedTunnelConfig(
                "Probe",
                "probe.conf",
                @"C:\temp\probe.conf",
                TunnelConfigFormat.AmneziaAwgNative,
                DateTimeOffset.UtcNow,
                "raw",
                null,
                config),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }

    private sealed class FakeRuntimeEnvironment : IRuntimeEnvironment
    {
        public bool IsWindows => true;

        public bool IsMacOS => false;
    }

    private sealed class RecordingDaemonTransport : IAmneziaDaemonTransport
    {
        public bool Available { get; set; }

        public JsonDocument? NextResponse { get; set; }

        public List<JsonObject> SentPayloads { get; } = [];

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Available);
        }

        public Task SendAsync(JsonObject payload, CancellationToken cancellationToken = default)
        {
            SentPayloads.Add((JsonObject)payload.DeepClone());
            return Task.CompletedTask;
        }

        public Task<JsonDocument> RequestAsync(JsonObject payload, CancellationToken cancellationToken = default)
        {
            SentPayloads.Add((JsonObject)payload.DeepClone());
            return Task.FromResult(NextResponse ?? JsonDocument.Parse("""{"type":"status","connected":false}"""));
        }
    }
}
