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
            new MacosNoOpKillSwitchService(),
            new FakeRuntimeEnvironment(),
            NullLogger<MacosVpnRuntimeAdapter>.Instance);

        var state = await adapter.ConnectAsync(BuildProfile());

        Assert.Equal(RuntimeConnectionStatus.Unsupported, state.Status);
        Assert.Contains(state.Warnings, warning => warning.Contains("macOS runtime bridge", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Transport_IsUnavailableOutsideMacOs()
    {
        var transport = new UnixDomainSocketMacosRuntimeBridgeTransport("/tmp/etovpn.runtime.sock");

        Assert.False(await transport.IsAvailableAsync());
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

        var awgValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var config = new TunnelConfig(
            TunnelConfigFormat.WireGuardConf,
            "raw",
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
                "raw",
                null,
                config),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }

    private sealed class FakeRuntimeEnvironment : IRuntimeEnvironment
    {
        public bool IsWindows => false;

        public bool IsMacOS => false;
    }

    private sealed class RecordingMacosTransport : IMacosRuntimeBridgeTransport
    {
        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);

        public Task SendAsync(System.Text.Json.Nodes.JsonObject payload, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<System.Text.Json.JsonDocument> RequestAsync(System.Text.Json.Nodes.JsonObject payload, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(System.Text.Json.JsonDocument.Parse("""{"connected":false}"""));
        }
    }
}
