using VpnClient.Core.Models;
using VpnClient.Infrastructure.Runtime;
using Xunit;

namespace VpnClient.Tests.Runtime;

public sealed class ProgramDataAmneziaRuntimeConfigStoreTests
{
    [Fact]
    public async Task PrepareAsync_MaterializesLegacyVpnProfileBeforeWritingRuntimeConfig()
    {
        var directory = Path.Combine(Path.GetTempPath(), "vpn-runtime-config-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        var store = new ProgramDataAmneziaRuntimeConfigStore(directory);
        var profile = new ImportedServerProfile(
            Guid.Parse("2148ddbe-76df-43c8-9ba0-4c75a7e07111"),
            "stoun_amnezia_config",
            new ImportedTunnelConfig(
                "stoun_amnezia_config",
                "stoun_amnezia_config.vpn",
                @"C:\temp\stoun_amnezia_config.vpn",
                TunnelConfigFormat.AmneziaVpn,
                DateTimeOffset.UtcNow,
                "vpn://payload",
                """
                {
                  "dns1": "1.1.1.1",
                  "dns2": "1.0.0.1",
                  "containers": [
                    {
                      "awg": {
                        "last_config": "{\"mtu\":\"1376\",\"config\":\"[Interface]\\nAddress = 10.8.1.15/32\\nDNS = $PRIMARY_DNS, $SECONDARY_DNS\\nPrivateKey = secret\\n\\n[Peer]\\nPublicKey = server\\nPresharedKey = psk\\nAllowedIPs = 0.0.0.0/0, ::/0\\nEndpoint = 37.1.197.163:45393\\nPersistentKeepalive = 25\\n\"}"
                      }
                    }
                  ]
                }
                """,
                new TunnelConfig(
                    TunnelConfigFormat.AmneziaVpn,
                    """
                    [Interface]
                    Address = 10.8.1.15/32
                    DNS = $PRIMARY_DNS, $SECONDARY_DNS
                    PrivateKey = secret

                    [Peer]
                    PublicKey = server
                    PresharedKey = psk
                    AllowedIPs = 0.0.0.0/0, ::/0
                    Endpoint = 37.1.197.163:45393
                    PersistentKeepalive = 25
                    """,
                    Array.Empty<ConfigLine>(),
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                    "10.8.1.15/32",
                    new[] { "$PRIMARY_DNS", "$SECONDARY_DNS" },
                    null,
                    new[] { "0.0.0.0/0", "::/0" },
                    25,
                    "37.1.197.163:45393",
                    "server",
                    "psk")),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        var prepared = await store.PrepareAsync(profile);
        var runtimeConfig = await File.ReadAllTextAsync(prepared.ConfigPath);

        Assert.Equal("vpn_stoun_amnezia_conf_2148dd", prepared.TunnelName);
        Assert.Contains("DNS = 1.1.1.1, 1.0.0.1", runtimeConfig);
        Assert.Contains("MTU = 1376", runtimeConfig);
        Assert.DoesNotContain("$PRIMARY_DNS", runtimeConfig);
        Assert.DoesNotContain("$SECONDARY_DNS", runtimeConfig);
    }
}
