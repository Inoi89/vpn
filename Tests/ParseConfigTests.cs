using VpnClient.Infrastructure.Services;
using Xunit;

public class ParseConfigTests
{
    [Fact]
    public void ParseConfig_ExtractsFields()
    {
        var config = """
[Interface]
Address = 10.8.1.2/32
DNS = 1.1.1.1,8.8.8.8
PrivateKey = key

[Peer]
AllowedIPs = 0.0.0.0/0, ::/0
PublicKey = pk
""";
        VpnServiceTestHelper.Parse(config, out var address, out var dns, out var allowed);
        Assert.Equal("10.8.1.2/32", address);
        Assert.Equal(new[] { "1.1.1.1", "8.8.8.8" }, dns);
        Assert.Equal(new[] { "0.0.0.0/0", "::/0" }, allowed);
    }
}

internal static class VpnServiceTestHelper
{
    public static void Parse(string config, out string? address, out System.Collections.Generic.List<string> dns, out System.Collections.Generic.List<string> allowed)
    {
        // call internal method via reflection since ParseConfig is private
        var type = typeof(VpnService);
        var method = type.GetMethod("ParseConfig", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        object?[] args = new object?[] { config, null!, null!, null! };
        method!.Invoke(null, args);
        address = (string?)args[1];
        dns = (System.Collections.Generic.List<string>)args[2]!;
        allowed = (System.Collections.Generic.List<string>)args[3]!;
    }
}
