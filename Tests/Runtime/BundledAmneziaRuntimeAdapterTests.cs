using Microsoft.Extensions.Logging.Abstractions;
using VpnClient.Core.Models;
using VpnClient.Infrastructure.Runtime;
using Xunit;

namespace VpnClient.Tests.Runtime;

public sealed class BundledAmneziaRuntimeAdapterTests
{
    [Fact]
    public async Task ConnectAsync_InstallsTunnelService_AndReadsAwgStatus()
    {
        var executor = new RecordingRuntimeCommandExecutor(
            new RuntimeCommandResult(0, string.Empty, string.Empty),
            new RuntimeCommandResult(
                0,
                """
                yvc_test	private	443	off	0	0	0	off
                server-pub	hidden	5.19.3.217:8271	10.8.1.2/32	1773782560	2404	8948	off
                """,
                string.Empty));

        var store = new RecordingAmneziaRuntimeConfigStore();
        var adapter = CreateAdapter(executor, store, new FakeWindowsRuntimeAssetLocator());

        var state = await adapter.ConnectAsync(BuildProfile());

        Assert.Equal(RuntimeConnectionStatus.Connected, state.Status);
        Assert.True(state.TunnelActive);
        Assert.Equal("5.19.3.217:8271", state.Endpoint);
        Assert.Equal(2404, state.ReceivedBytes);
        Assert.Equal(8948, state.SentBytes);
        Assert.Contains(executor.Calls, call =>
            call.FileName.Equals(@"C:\bundle\runtime\wireguard\amneziawg.exe", StringComparison.OrdinalIgnoreCase)
            && call.Arguments.SequenceEqual(["/installtunnelservice", @"C:\ProgramData\YourVpnClient\Runtime\Configurations\yvc_test.conf"]));
        Assert.Contains(executor.Calls, call =>
            call.FileName.Equals(@"C:\bundle\runtime\wireguard\awg.exe", StringComparison.OrdinalIgnoreCase)
            && call.Arguments.SequenceEqual(["show", "yvc_test", "dump"]));
        Assert.Equal(1, store.PrepareCount);
    }

    [Fact]
    public async Task DisconnectAsync_UninstallsTunnelService_AndDeletesPreparedConfig()
    {
        var executor = new RecordingRuntimeCommandExecutor(
            new RuntimeCommandResult(0, string.Empty, string.Empty),
            new RuntimeCommandResult(
                0,
                """
                yvc_test	private	443	off	0	0	0	off
                server-pub	hidden	5.19.3.217:8271	10.8.1.2/32	1773782560	2404	8948	off
                """,
                string.Empty),
            new RuntimeCommandResult(0, string.Empty, string.Empty));

        var store = new RecordingAmneziaRuntimeConfigStore();
        var adapter = CreateAdapter(executor, store, new FakeWindowsRuntimeAssetLocator());

        await adapter.ConnectAsync(BuildProfile());
        var state = await adapter.DisconnectAsync();

        Assert.Equal(RuntimeConnectionStatus.Disconnected, state.Status);
        Assert.Equal(1, store.DeleteCount);
        Assert.Contains(executor.Calls, call =>
            call.FileName.Equals(@"C:\bundle\runtime\wireguard\amneziawg.exe", StringComparison.OrdinalIgnoreCase)
            && call.Arguments.SequenceEqual(["/uninstalltunnelservice", "yvc_test"]));
    }

    [Fact]
    public async Task ConnectAsync_ReturnsUnsupported_WhenBundledRuntimeIsMissing()
    {
        var executor = new RecordingRuntimeCommandExecutor();
        var store = new RecordingAmneziaRuntimeConfigStore();
        var adapter = CreateAdapter(executor, store, new MissingWindowsRuntimeAssetLocator());

        var state = await adapter.ConnectAsync(BuildProfile());

        Assert.Equal(RuntimeConnectionStatus.Unsupported, state.Status);
        Assert.Empty(executor.Calls);
        Assert.Equal(0, store.PrepareCount);
    }

    private static BundledAmneziaRuntimeAdapter CreateAdapter(
        RecordingRuntimeCommandExecutor executor,
        RecordingAmneziaRuntimeConfigStore store,
        IWindowsRuntimeAssetLocator assetLocator)
    {
        return new BundledAmneziaRuntimeAdapter(
            executor,
            new FakeRuntimeEnvironment(),
            assetLocator,
            store,
            NullLogger<BundledAmneziaRuntimeAdapter>.Instance);
    }

    private static ImportedServerProfile BuildProfile()
    {
        var interfaceValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Address"] = "10.8.1.2/32",
            ["DNS"] = "8.8.8.8, 8.8.4.4",
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
            ["Endpoint"] = "45.136.49.191:443",
            ["PersistentKeepalive"] = "25"
        };

        var config = new TunnelConfig(
            TunnelConfigFormat.AmneziaAwgNative,
            """
            [Interface]
            Address = 10.8.1.2/32
            DNS = 8.8.8.8, 8.8.4.4
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
            Endpoint = 45.136.49.191:443
            PersistentKeepalive = 25
            """,
            [],
            interfaceValues,
            peerValues,
            awgValues,
            "10.8.1.2/32",
            ["8.8.8.8", "8.8.4.4"],
            "1376",
            ["0.0.0.0/0", "::/0"],
            25,
            "45.136.49.191:443",
            "server-pub",
            "hidden");

        return new ImportedServerProfile(
            Guid.Parse("11111111-2222-3333-4444-555555555555"),
            "Test bundled",
            new ImportedTunnelConfig(
                "Test bundled",
                "test.conf",
                @"C:\temp\test.conf",
                TunnelConfigFormat.AmneziaAwgNative,
                DateTimeOffset.UtcNow,
                config.RawConfig,
                null,
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
        public string WgExecutablePath => AwgExecutablePath;
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

    private sealed class MissingWindowsRuntimeAssetLocator : IWindowsRuntimeAssetLocator
    {
        public string ApplicationBaseDirectory => @"C:\bundle";
        public string RuntimeRootDirectory => @"C:\bundle\runtime";
        public string WireGuardRuntimeDirectory => @"C:\bundle\runtime\wireguard";
        public string AmneziaWgExecutablePath => "amneziawg.exe";
        public string AwgExecutablePath => "awg.exe";
        public string WgExecutablePath => "awg.exe";
        public string WintunDllPath => "wintun.dll";
        public string? WireGuardServiceExecutablePath => null;
        public string? TunnelDllPath => null;
        public string? WireGuardDllPath => null;
        public bool HasBundledAmneziaWgExecutable => false;
        public bool HasBundledAwgExecutable => false;
        public bool HasBundledWgExecutable => false;
        public bool HasBundledWintun => false;

        public IReadOnlyList<string> GetWarnings() => ["Missing bundled runtime."];
    }

    private sealed class RecordingRuntimeCommandExecutor(params RuntimeCommandResult[] results) : IRuntimeCommandExecutor
    {
        private readonly Queue<RuntimeCommandResult> _results = new(results);

        public List<RecordedCommand> Calls { get; } = [];

        public Task<RuntimeCommandResult> ExecuteAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken = default)
        {
            Calls.Add(new RecordedCommand(fileName, arguments.ToArray()));
            return Task.FromResult(_results.Count > 0 ? _results.Dequeue() : new RuntimeCommandResult(0, string.Empty, string.Empty));
        }
    }

    private sealed record RecordedCommand(string FileName, string[] Arguments);

    private sealed class RecordingAmneziaRuntimeConfigStore : IAmneziaRuntimeConfigStore
    {
        public int PrepareCount { get; private set; }

        public int DeleteCount { get; private set; }

        public PreparedTunnelProfile Describe(ImportedServerProfile profile)
        {
            return new PreparedTunnelProfile(profile.Id, profile.DisplayName, "yvc_test", @"C:\ProgramData\YourVpnClient\Runtime\Configurations\yvc_test.conf");
        }

        public Task<PreparedTunnelProfile> PrepareAsync(ImportedServerProfile profile, CancellationToken cancellationToken = default)
        {
            PrepareCount++;
            return Task.FromResult(Describe(profile));
        }

        public Task DeleteAsync(PreparedTunnelProfile preparedProfile, CancellationToken cancellationToken = default)
        {
            DeleteCount++;
            return Task.CompletedTask;
        }
    }
}
