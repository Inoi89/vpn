using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using VpnClient.Core.Interfaces;
using VpnClient.Core.Models;
using VpnClient.Infrastructure.Runtime;
using Xunit;

namespace VpnClient.Tests.Runtime;

public sealed class HybridVpnRuntimeAdapterTests
{
    [Fact]
    public async Task ConnectAsync_PrefersDaemonWhenAvailable()
    {
        var transport = new RecordingDaemonTransport
        {
            Available = true,
            NextResponse = BuildConnectedStatus()
        };

        var bundledExecutor = new RecordingRuntimeCommandExecutor();
        var bundled = new BundledAmneziaRuntimeAdapter(
            bundledExecutor,
            new FakeRuntimeEnvironment(),
            new FakeWindowsRuntimeAssetLocator(),
            new RecordingAmneziaRuntimeConfigStore(),
            NullLogger<BundledAmneziaRuntimeAdapter>.Instance);

        var daemon = new AmneziaDaemonRuntimeAdapter(
            transport,
            new FakeRuntimeEnvironment(),
            NullLogger<AmneziaDaemonRuntimeAdapter>.Instance);

        var fallback = new WindowsFirstVpnRuntimeAdapter(
            new RecordingWintunService(),
            new RecordingRuntimeCommandExecutor(),
            new FakeRuntimeEnvironment(),
            new FakeWindowsRuntimeAssetLocator(),
            NullLogger<WindowsFirstVpnRuntimeAdapter>.Instance);

        var adapter = new HybridVpnRuntimeAdapter(bundled, daemon, fallback, transport);

        var state = await adapter.ConnectAsync(BuildProfile());

        Assert.Equal("AmneziaDaemon", state.AdapterName);
        Assert.Equal(RuntimeConnectionStatus.Connected, state.Status);
        Assert.Empty(bundledExecutor.Calls);
        Assert.Contains(transport.SentPayloads, payload => payload["type"]!.GetValue<string>() == "activate");
    }

    [Fact]
    public async Task TryRestoreAsync_PrefersDaemonRestoreWhenAvailable()
    {
        var transport = new RecordingDaemonTransport
        {
            Available = true,
            NextResponse = BuildConnectedStatus()
        };

        var bundledExecutor = new RecordingRuntimeCommandExecutor();
        var bundled = new BundledAmneziaRuntimeAdapter(
            bundledExecutor,
            new FakeRuntimeEnvironment(),
            new FakeWindowsRuntimeAssetLocator(),
            new RecordingAmneziaRuntimeConfigStore(),
            NullLogger<BundledAmneziaRuntimeAdapter>.Instance);

        var daemon = new AmneziaDaemonRuntimeAdapter(
            transport,
            new FakeRuntimeEnvironment(),
            NullLogger<AmneziaDaemonRuntimeAdapter>.Instance);

        var fallback = new WindowsFirstVpnRuntimeAdapter(
            new RecordingWintunService(),
            new RecordingRuntimeCommandExecutor(),
            new FakeRuntimeEnvironment(),
            new FakeWindowsRuntimeAssetLocator(),
            NullLogger<WindowsFirstVpnRuntimeAdapter>.Instance);

        var adapter = new HybridVpnRuntimeAdapter(bundled, daemon, fallback, transport);

        var state = await adapter.TryRestoreAsync([BuildProfile()]);

        Assert.Equal("AmneziaDaemon", state.AdapterName);
        Assert.Equal(RuntimeConnectionStatus.Connected, state.Status);
        Assert.Empty(bundledExecutor.Calls);
    }

    private static JsonDocument BuildConnectedStatus()
    {
        return JsonDocument.Parse("""
            {
              "type": "status",
              "connected": true,
              "serverIpv4Gateway": "37.1.197.163",
              "serverPort": 45393,
              "deviceIpv4Address": "10.8.1.15/32",
              "date": "Tue Mar 19 15:57:00 2026",
              "txBytes": 128,
              "rxBytes": 512
            }
            """);
    }

    private static ImportedServerProfile BuildProfile()
    {
        var interfaceValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Address"] = "10.8.1.15/32",
            ["DNS"] = "1.1.1.1, 1.0.0.1",
            ["MTU"] = "1376",
            ["PrivateKey"] = "client-private-key",
            ["Jc"] = "3",
            ["Jmin"] = "10",
            ["Jmax"] = "50",
            ["S1"] = "106",
            ["S2"] = "17",
            ["H1"] = "891876870",
            ["H2"] = "1202676760",
            ["H3"] = "479848242",
            ["H4"] = "1400026040"
        };

        var awgValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Jc"] = "3",
            ["Jmin"] = "10",
            ["Jmax"] = "50",
            ["S1"] = "106",
            ["S2"] = "17",
            ["H1"] = "891876870",
            ["H2"] = "1202676760",
            ["H3"] = "479848242",
            ["H4"] = "1400026040"
        };

        var peerValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["PublicKey"] = "server-pub",
            ["PresharedKey"] = "hidden",
            ["AllowedIPs"] = "0.0.0.0/0, ::/0",
            ["Endpoint"] = "37.1.197.163:45393",
            ["PersistentKeepalive"] = "25"
        };

        var config = new TunnelConfig(
            TunnelConfigFormat.AmneziaVpn,
            """
            [Interface]
            Address = 10.8.1.15/32
            DNS = 1.1.1.1, 1.0.0.1
            PrivateKey = client-private-key
            MTU = 1376
            Jc = 3
            Jmin = 10
            Jmax = 50
            S1 = 106
            S2 = 17
            H1 = 891876870
            H2 = 1202676760
            H3 = 479848242
            H4 = 1400026040

            [Peer]
            PublicKey = server-pub
            PresharedKey = hidden
            AllowedIPs = 0.0.0.0/0, ::/0
            Endpoint = 37.1.197.163:45393
            PersistentKeepalive = 25
            """,
            [],
            interfaceValues,
            peerValues,
            awgValues,
            "10.8.1.15/32",
            ["1.1.1.1", "1.0.0.1"],
            "1376",
            ["0.0.0.0/0", "::/0"],
            25,
            "37.1.197.163:45393",
            "server-pub",
            "hidden");

        return new ImportedServerProfile(
            Guid.Parse("2148ddbe-76df-43c8-9ba0-4c75a7e07111"),
            "stoun_amnezia_config",
            new ImportedTunnelConfig(
                "stoun_amnezia_config",
                "stoun_amnezia_config.vpn",
                @"C:\temp\stoun_amnezia_config.vpn",
                TunnelConfigFormat.AmneziaVpn,
                DateTimeOffset.UtcNow,
                "vpn://payload",
                """{"dns1":"1.1.1.1","dns2":"1.0.0.1"}""",
                config),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }

    private sealed class FakeRuntimeEnvironment : IRuntimeEnvironment
    {
        public bool IsWindows => true;
    }

    private sealed class FakeWindowsRuntimeAssetLocator : IWindowsRuntimeAssetLocator
    {
        public string ApplicationBaseDirectory => @"C:\bundle";
        public string RuntimeRootDirectory => @"C:\bundle\runtime";
        public string WireGuardRuntimeDirectory => @"C:\bundle\runtime\wireguard";
        public string AmneziaWgExecutablePath => @"C:\bundle\runtime\wireguard\amneziawg.exe";
        public string AwgExecutablePath => @"C:\bundle\runtime\wireguard\awg.exe";
        public string WgExecutablePath => @"C:\bundle\runtime\wireguard\awg.exe";
        public string WintunDllPath => @"C:\bundle\runtime\wireguard\wintun.dll";
        public string? WireGuardServiceExecutablePath => null;
        public string? TunnelDllPath => null;
        public string? WireGuardDllPath => null;
        public bool HasBundledAmneziaWgExecutable => true;
        public bool HasBundledAwgExecutable => true;
        public bool HasBundledWgExecutable => true;
        public bool HasBundledWintun => true;

        public IReadOnlyList<string> GetWarnings() => [];
    }

    private sealed class RecordingRuntimeCommandExecutor(params RuntimeCommandResult[] results) : IRuntimeCommandExecutor
    {
        private readonly Queue<RuntimeCommandResult> _results = new(results);

        public List<RecordedCommand> Calls { get; } = [];

        public Task<RuntimeCommandResult> ExecuteAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
        {
            Calls.Add(new RecordedCommand(fileName, arguments.ToArray()));
            return Task.FromResult(_results.Count > 0 ? _results.Dequeue() : new RuntimeCommandResult(0, string.Empty, string.Empty));
        }
    }

    private sealed record RecordedCommand(string FileName, string[] Arguments);

    private sealed class RecordingAmneziaRuntimeConfigStore : IAmneziaRuntimeConfigStore
    {
        public PreparedTunnelProfile Describe(ImportedServerProfile profile)
        {
            return new PreparedTunnelProfile(profile.Id, profile.DisplayName, "vpn_stoun_2148dd", @"C:\ProgramData\YourVpnClient\Runtime\Configurations\vpn_stoun_2148dd.conf");
        }

        public Task<PreparedTunnelProfile> PrepareAsync(ImportedServerProfile profile, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Describe(profile));
        }

        public Task DeleteAsync(PreparedTunnelProfile preparedProfile, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingWintunService : IWintunService
    {
        public Task CreateAdapterAsync(string name) => Task.CompletedTask;

        public Task DeleteAdapterAsync(string name) => Task.CompletedTask;
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
