using VpnClient.Core.Models.Diagnostics;
using VpnClient.Infrastructure.Diagnostics;
using Xunit;

namespace VpnClient.Tests.Diagnostics;

public sealed class WindowsWireGuardDumpParserTests
{
    [Fact]
    public void Parse_AggregatesPeerTrafficAndLatestHandshake()
    {
        var observedAt = new DateTimeOffset(2026, 3, 18, 10, 0, 0, TimeSpan.Zero);
        var dump = """
privatekey	publickey	listen-port	fwmark
peer-one	psk-one	198.51.100.10:1234	10.8.0.2/32	1710000100	100	200	25
peer-two	psk-two	198.51.100.11:1234	10.8.0.3/32	1710000200	300	400	25
""";

        var snapshot = WindowsWireGuardDumpParser.Parse(dump, observedAt);

        Assert.NotNull(snapshot);
        Assert.Equal(2, snapshot!.PeerCount);
        Assert.Equal(400, snapshot.TotalBytesReceived);
        Assert.Equal(600, snapshot.TotalBytesSent);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1710000200), snapshot.LastHandshakeUtc);
        Assert.Equal(observedAt, snapshot.ObservedAtUtc);
    }

    [Fact]
    public void Parse_ReturnsNullForEmptyDump()
    {
        var snapshot = WindowsWireGuardDumpParser.Parse(string.Empty, DateTimeOffset.UtcNow);

        Assert.Null(snapshot);
    }
}
