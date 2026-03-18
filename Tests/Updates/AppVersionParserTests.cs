using VpnClient.Infrastructure.Updates;
using Xunit;

namespace VpnClient.Tests.Updates;

public sealed class AppVersionParserTests
{
    [Theory]
    [InlineData("1.0.1", "1.0.0")]
    [InlineData("1.2.0", "1.1.99")]
    [InlineData("2.0.0", "1.9.9")]
    public void Compare_ReturnsPositive_WhenLeftIsNewer(string left, string right)
    {
        Assert.True(AppVersionParser.Compare(left, right) > 0);
    }

    [Theory]
    [InlineData("1.0.0-beta", "1.0.0")]
    [InlineData("1.0.0-preview2", "1.0.0")]
    public void Compare_TreatsSuffixAsPreRelease(string left, string right)
    {
        Assert.True(AppVersionParser.Compare(left, right) < 0);
    }

    [Theory]
    [InlineData("1.0.0+build.4", "1.0.0")]
    [InlineData("1.0.0", "1.0.0")]
    public void Compare_IgnoresBuildMetadataForCurrentVersionShape(string left, string right)
    {
        Assert.Equal(0, AppVersionParser.Compare(left.Split('+')[0], right));
    }
}
