using Microsoft.Extensions.Logging.Abstractions;
using VpnClient.Core.Interfaces;
using VpnClient.Core.Models;
using VpnClient.Infrastructure.Runtime;
using Xunit;

namespace VpnClient.Tests.Runtime;

public sealed class WindowsFirstVpnRuntimeAdapterTests
{
    [Fact]
    public async Task ConnectAsync_AppliesExplicitRuntimeCommands_WithoutSetConf()
    {
        var executor = new RecordingRuntimeCommandExecutor(
            new RuntimeCommandResult(0, string.Empty, string.Empty));
        var wintun = new RecordingWintunService();
        var adapter = CreateAdapter(executor, wintun);

        var state = await adapter.ConnectAsync(BuildProfile(includeAwg: true));

        Assert.Equal(RuntimeConnectionStatus.Degraded, state.Status);
        Assert.Contains(state.Warnings, warning => warning.Contains("AWG-specific runtime keys", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(executor.Calls, call => call.FileName.Equals(@"C:\bundle\runtime\wireguard\awg.exe", StringComparison.OrdinalIgnoreCase) && call.Arguments.Contains("set", StringComparer.OrdinalIgnoreCase));
        Assert.DoesNotContain(executor.Calls, call => call.Arguments.Any(argument => argument.Contains("setconf", StringComparison.OrdinalIgnoreCase)));
        Assert.Contains(executor.Calls, call => call.FileName.Equals("netsh", StringComparison.OrdinalIgnoreCase) && call.Arguments.Contains("dns", StringComparer.OrdinalIgnoreCase));
        Assert.Equal(1, wintun.CreateCount);
        Assert.Equal(0, wintun.DeleteCount);
    }

    [Fact]
    public async Task GetStatusAsync_ParsesDumpAndUpdatesHandshakeMetrics()
    {
        var executor = new RecordingRuntimeCommandExecutor(
            new RuntimeCommandResult(0, string.Empty, string.Empty),
            new RuntimeCommandResult(0, string.Empty, string.Empty),
            new RuntimeCommandResult(0, string.Empty, string.Empty),
            new RuntimeCommandResult(0, string.Empty, string.Empty),
            new RuntimeCommandResult(0, string.Empty, string.Empty),
            new RuntimeCommandResult(0, string.Empty, string.Empty),
            new RuntimeCommandResult(0, string.Empty, string.Empty),
            new RuntimeCommandResult(0, string.Empty, string.Empty),
            new RuntimeCommandResult(
                0,
                """
                wg0	hidden	none	none	0	0	0	off
                z3/zMVGQK9zjSp27mrg8IE4oexDxXTk52h355oXF3x8=	hidden	5.19.3.217:8271	10.8.1.2/32	1773782560	2404	8948	off
                """,
                string.Empty));
        var wintun = new RecordingWintunService();
        var adapter = CreateAdapter(executor, wintun);

        await adapter.ConnectAsync(BuildProfile());
        var state = await adapter.GetStatusAsync();

        Assert.Equal(RuntimeConnectionStatus.Connected, state.Status);
        Assert.True(state.TunnelActive);
        Assert.Equal("5.19.3.217:8271", state.Endpoint);
        Assert.Equal(2404, state.ReceivedBytes);
        Assert.Equal(8948, state.SentBytes);
        Assert.Equal(new DateTimeOffset(2026, 3, 17, 21, 22, 40, TimeSpan.Zero), state.LatestHandshakeAtUtc);
    }

    [Fact]
    public async Task DisconnectAsync_DeletesAdapterAndReturnsDisconnectedState()
    {
        var executor = new RecordingRuntimeCommandExecutor(
            new RuntimeCommandResult(0, string.Empty, string.Empty));
        var wintun = new RecordingWintunService();
        var adapter = CreateAdapter(executor, wintun);

        await adapter.ConnectAsync(BuildProfile());
        var disconnected = await adapter.DisconnectAsync();

        Assert.Equal(RuntimeConnectionStatus.Disconnected, disconnected.Status);
        Assert.Equal(1, wintun.DeleteCount);
    }

    [Fact]
    public async Task ConnectAsync_RejectsMissingRequiredFields()
    {
        var executor = new RecordingRuntimeCommandExecutor(
            new RuntimeCommandResult(0, string.Empty, string.Empty));
        var wintun = new RecordingWintunService();
        var adapter = CreateAdapter(executor, wintun);

        var profile = new ImportedServerProfile(
            Guid.NewGuid(),
            "broken",
            new ImportedTunnelConfig(
                "broken",
                "broken.conf",
                @"C:\temp\broken.conf",
                TunnelConfigFormat.WireGuardConf,
                DateTimeOffset.UtcNow,
                """
                [Interface]
                DNS = 8.8.8.8

                [Peer]
                AllowedIPs = 0.0.0.0/0
                """,
                null,
                new TunnelConfig(
                    TunnelConfigFormat.WireGuardConf,
                    """
                    [Interface]
                    DNS = 8.8.8.8

                    [Peer]
                    AllowedIPs = 0.0.0.0/0
                    """,
                    [],
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["DNS"] = "8.8.8.8"
                    },
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["AllowedIPs"] = "0.0.0.0/0"
                    },
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                    null,
                    ["8.8.8.8"],
                    null,
                    ["0.0.0.0/0"],
                    null,
                    null,
                    null,
                    null)),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        var state = await adapter.ConnectAsync(profile);

        Assert.Equal(RuntimeConnectionStatus.Failed, state.Status);
        Assert.Empty(executor.Calls);
        Assert.Equal(0, wintun.CreateCount);
    }

    private static WindowsFirstVpnRuntimeAdapter CreateAdapter(
        RecordingRuntimeCommandExecutor executor,
        RecordingWintunService wintun)
    {
        return new WindowsFirstVpnRuntimeAdapter(
            wintun,
            executor,
            new FakeRuntimeEnvironment(),
            new FakeWindowsRuntimeAssetLocator(),
            NullLogger<WindowsFirstVpnRuntimeAdapter>.Instance);
    }

    private static ImportedServerProfile BuildProfile(bool includeAwg = false)
    {
        var interfaceValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Address"] = "10.8.1.2/32",
            ["DNS"] = "8.8.8.8, 8.8.4.4",
            ["MTU"] = "1280",
            ["PrivateKey"] = "client-private-key"
        };

        var awgValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (includeAwg)
        {
            awgValues["Jc"] = "2";
            awgValues["Jmin"] = "10";
            awgValues["Jmax"] = "50";
            awgValues["S1"] = "94";
            awgValues["S2"] = "146";
            awgValues["H1"] = "2097057167";
            awgValues["H2"] = "2385741147";
            awgValues["H3"] = "3630987908";
            awgValues["H4"] = "283091219";
            foreach (var pair in awgValues)
            {
                interfaceValues[pair.Key] = pair.Value;
            }
        }

        var peerValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["PublicKey"] = "z3/zMVGQK9zjSp27mrg8IE4oexDxXTk52h355oXF3x8=",
            ["PresharedKey"] = "hidden",
            ["AllowedIPs"] = "0.0.0.0/0, ::/0",
            ["Endpoint"] = "45.136.49.191:45393",
            ["PersistentKeepalive"] = "25"
        };

        var config = new TunnelConfig(
            includeAwg ? TunnelConfigFormat.AmneziaAwgNative : TunnelConfigFormat.WireGuardConf,
            "raw",
            [],
            interfaceValues,
            peerValues,
            awgValues,
            "10.8.1.2/32",
            ["8.8.8.8", "8.8.4.4"],
            "1280",
            ["0.0.0.0/0", "::/0"],
            25,
            "45.136.49.191:45393",
            "z3/zMVGQK9zjSp27mrg8IE4oexDxXTk52h355oXF3x8=",
            "hidden");

        return new ImportedServerProfile(
            Guid.NewGuid(),
            includeAwg ? "AWG" : "WG",
            new ImportedTunnelConfig(
                includeAwg ? "AWG" : "WG",
                includeAwg ? "awg.conf" : "wg.conf",
                @"C:\temp\sample.conf",
                includeAwg ? TunnelConfigFormat.AmneziaAwgNative : TunnelConfigFormat.WireGuardConf,
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

        public IReadOnlyList<string> GetWarnings()
        {
            return [];
        }
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

    private sealed class RecordingWintunService : IWintunService
    {
        public int CreateCount { get; private set; }

        public int DeleteCount { get; private set; }

        public Task CreateAdapterAsync(string name)
        {
            CreateCount++;
            return Task.CompletedTask;
        }

        public Task DeleteAdapterAsync(string name)
        {
            DeleteCount++;
            return Task.CompletedTask;
        }
    }
}
