using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using VpnClient.Infrastructure.Services;
using Xunit;

public sealed class ConfigServiceTests
{
    [Fact]
    public async Task ImportConfigAsync_LoadsNativeConfProfile()
    {
        var tempDirectory = CreateTempDirectory();
        var configPath = Path.Combine(tempDirectory, "sample.conf");
        var statePath = Path.Combine(tempDirectory, "profile.json");

        await File.WriteAllTextAsync(configPath,
            """
            [Interface]
            Address = 10.8.1.10/32
            DNS = 8.8.8.8, 8.8.4.4
            PrivateKey = secret

            [Peer]
            PublicKey = server
            AllowedIPs = 0.0.0.0/0, ::/0
            Endpoint = 5.61.37.29:51820
            """);

        var service = new ConfigService(statePath);

        var profile = await service.ImportConfigAsync(configPath);

        Assert.Equal("sample", profile.DisplayName);
        Assert.Equal("AmneziaWG (.conf)", profile.Format);
        Assert.Equal("5.61.37.29:51820", profile.Endpoint);
        Assert.Equal("10.8.1.10/32", profile.Address);
        Assert.Equal("8.8.8.8", profile.PrimaryDns);
        Assert.Contains("[Interface]", profile.RawConfig);
    }

    [Fact]
    public async Task ImportConfigAsync_DecodesVpnPayloadIntoRawConfig()
    {
        var tempDirectory = CreateTempDirectory();
        var configPath = Path.Combine(tempDirectory, "sample.vpn");
        var statePath = Path.Combine(tempDirectory, "profile.json");
        var rawConfig =
            """
            [Interface]
            Address = 10.8.1.11/32
            DNS = 1.1.1.1, 1.0.0.1
            PrivateKey = secret

            [Peer]
            PublicKey = server
            AllowedIPs = 0.0.0.0/0, ::/0
            Endpoint = 45.136.49.191:45393
            """;

        await File.WriteAllTextAsync(configPath, BuildVpnPayload(rawConfig));
        var service = new ConfigService(statePath);

        var profile = await service.ImportConfigAsync(configPath);

        Assert.Equal("sample", profile.DisplayName);
        Assert.Equal("Amnezia VPN (.vpn)", profile.Format);
        Assert.Equal("45.136.49.191:45393", profile.Endpoint);
        Assert.Equal("10.8.1.11/32", profile.Address);
        Assert.Equal("1.1.1.1", profile.PrimaryDns);
        Assert.Contains("PrivateKey = secret", profile.RawConfig);
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "vpn-client-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string BuildVpnPayload(string rawConfig)
    {
        var payloadObject = new
        {
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

        var json = JsonSerializer.Serialize(payloadObject);
        var jsonBytes = Encoding.UTF8.GetBytes(json);

        using var compressed = new MemoryStream();
        compressed.Write(new byte[4]);
        using (var zlibStream = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            zlibStream.Write(jsonBytes, 0, jsonBytes.Length);
        }

        compressed.Position = 0;
        var prefix = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(prefix, (uint)jsonBytes.Length);
        compressed.Write(prefix, 0, prefix.Length);

        var bytes = compressed.ToArray();
        var base64 = Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        return $"vpn://{base64}";
    }
}
