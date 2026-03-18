using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using VpnClient.Core.Interfaces;
using VpnClient.Core.Models;
using VpnClient.Infrastructure.Import;
using Xunit;

public sealed class ImportServiceTests
{
    private readonly IImportService _service = new AmneziaImportService();

    [Fact]
    public async Task ImportAsync_ParsesNativeConfWithoutLosingFields()
    {
        var tempDirectory = CreateTempDirectory();
        var path = Path.Combine(tempDirectory, "native.conf");

        await File.WriteAllTextAsync(path,
            """
            [Interface]
            Address = 10.8.1.15/32
            DNS = 1.1.1.1, 1.0.0.1
            MTU = 1280
            PrivateKey = secret
            Jc = 3
            Jmin = 10
            Jmax = 50
            J7 = 77
            S1 = 84
            S2 = 45
            S3 = 12
            S4 = 99
            H1 = 1
            H2 = 2
            H3 = 3
            H4 = 4
            I1 = 11
            I2 = 12
            I3 = 13
            I4 = 14
            I5 = 15

            [Peer]
            PublicKey = server
            PresharedKey = psk
            AllowedIPs = 0.0.0.0/0, ::/0
            Endpoint = 5.61.40.132:40936
            PersistentKeepalive = 25
            """);

        var imported = await _service.ImportAsync(path);

        Assert.Equal(TunnelConfigFormat.AmneziaAwgNative, imported.SourceFormat);
        Assert.Equal("native", imported.DisplayName);
        Assert.Equal("10.8.1.15/32", imported.TunnelConfig.Address);
        Assert.Equal(new[] { "1.1.1.1", "1.0.0.1" }, imported.TunnelConfig.DnsServers);
        Assert.Equal("1280", imported.TunnelConfig.Mtu);
        Assert.Equal(new[] { "0.0.0.0/0", "::/0" }, imported.TunnelConfig.AllowedIps);
        Assert.Equal(25, imported.TunnelConfig.PersistentKeepalive);
        Assert.Equal("5.61.40.132:40936", imported.TunnelConfig.Endpoint);
        Assert.Equal("server", imported.TunnelConfig.PublicKey);
        Assert.Equal("psk", imported.TunnelConfig.PresharedKey);
        Assert.Equal("3", imported.TunnelConfig.AwgValues["Jc"]);
        Assert.Equal("77", imported.TunnelConfig.AwgValues["J7"]);
        Assert.Equal("15", imported.TunnelConfig.AwgValues["I5"]);
        Assert.Contains(imported.TunnelConfig.Lines, line => line.Kind == ConfigLineKind.SectionHeader && line.SectionName == "Interface");
    }

    [Fact]
    public async Task ImportAsync_ParsesVpnPackageAndKeepsMetadata()
    {
        var tempDirectory = CreateTempDirectory();
        var path = Path.Combine(tempDirectory, "amnezia.vpn");
        var rawConfig =
            """
            [Interface]
            Address = 10.8.1.16/32
            DNS = 8.8.8.8, 8.8.4.4
            MTU = 1280
            PrivateKey = secret
            Jc = 2
            Jmin = 10
            Jmax = 50
            J7 = 77
            S1 = 94
            S2 = 146
            H1 = 2097057167
            H2 = 2385741147
            H3 = 3630987908
            H4 = 283091219

            [Peer]
            PublicKey = server
            PresharedKey = psk
            AllowedIPs = 0.0.0.0/0, ::/0
            Endpoint = 45.136.49.191:45393
            PersistentKeepalive = 25
            """;

        await File.WriteAllTextAsync(path, BuildVpnFile(rawConfig, "My Server"));

        var imported = await _service.ImportAsync(path);

        Assert.Equal(TunnelConfigFormat.AmneziaVpn, imported.SourceFormat);
        Assert.Equal("My Server", imported.DisplayName);
        Assert.NotNull(imported.RawPackageJson);
        Assert.Contains("\"containers\"", imported.RawPackageJson);
        Assert.NotNull(imported.TunnelConfig.RawConfig);
        Assert.Contains("[Interface]", imported.TunnelConfig.RawConfig);
        Assert.Equal("10.8.1.16/32", imported.TunnelConfig.Address);
        Assert.Equal("45.136.49.191:45393", imported.TunnelConfig.Endpoint);
        Assert.Equal("8.8.8.8", imported.TunnelConfig.DnsServers[0]);
        Assert.Equal("77", imported.TunnelConfig.AwgValues["J7"]);
        Assert.Equal("146", imported.TunnelConfig.AwgValues["S2"]);
        Assert.Equal("283091219", imported.TunnelConfig.AwgValues["H4"]);
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "vpn-client-import-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string BuildVpnFile(string rawConfig, string displayName)
    {
        var package = new
        {
            display_name = displayName,
            containers = new[]
            {
                new
                {
                    awg = new
                    {
                        last_config = JsonSerializer.Serialize(new
                        {
                            config = rawConfig
                        })
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(package);
        var jsonBytes = Encoding.UTF8.GetBytes(json);

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            zlib.Write(jsonBytes, 0, jsonBytes.Length);
        }

        var compressedBytes = compressed.ToArray();
        var payload = new byte[4 + compressedBytes.Length];
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(0, 4), (uint)jsonBytes.Length);
        Buffer.BlockCopy(compressedBytes, 0, payload, 4, compressedBytes.Length);

        var base64 = Convert.ToBase64String(payload)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        return $"vpn://{base64}";
    }
}
