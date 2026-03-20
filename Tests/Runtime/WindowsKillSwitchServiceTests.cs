using VpnClient.Core.Interfaces;
using VpnClient.Infrastructure.Runtime;
using Xunit;

namespace VpnClient.Tests.Runtime;

public sealed class WindowsKillSwitchServiceTests
{
    [Fact]
    public async Task ArmAsync_AddsFirewallRules_ForIpEndpoint()
    {
        var executor = new RecordingRuntimeCommandExecutor();
        var service = new WindowsKillSwitchService(executor, new FakeRuntimeEnvironment());

        await service.ArmAsync("37.1.197.163:443");

        Assert.True(service.IsArmed);
        Assert.Contains(executor.Calls, call => call.Arguments.Contains("add") && call.Arguments.Contains("name=YourVpnClient Kill Switch - Allow VPN"));
        Assert.Contains(executor.Calls, call => call.Arguments.Contains("remoteip=37.1.197.163"));
        Assert.Contains(executor.Calls, call => call.Arguments.Contains("remoteport=443"));
        Assert.Contains(executor.Calls, call => call.Arguments.Contains("name=YourVpnClient Kill Switch - Block All"));
    }

    [Fact]
    public async Task ArmAsync_FallsBackToAnyRemoteIp_ForHostnameEndpoint()
    {
        var executor = new RecordingRuntimeCommandExecutor();
        var service = new WindowsKillSwitchService(executor, new FakeRuntimeEnvironment());

        await service.ArmAsync("vpn.example.com:51820");

        Assert.Contains(executor.Calls, call => call.Arguments.Contains("remoteip=any"));
        Assert.Contains(executor.Calls, call => call.Arguments.Contains("remoteport=51820"));
    }

    [Fact]
    public async Task DisarmAsync_DeletesRules()
    {
        var executor = new RecordingRuntimeCommandExecutor();
        var service = new WindowsKillSwitchService(executor, new FakeRuntimeEnvironment());

        await service.ArmAsync("37.1.197.163:443");
        executor.Calls.Clear();

        await service.DisarmAsync();

        Assert.False(service.IsArmed);
        Assert.Equal(3, executor.Calls.Count);
        Assert.All(executor.Calls, call => Assert.Contains("delete", call.Arguments));
    }

    private sealed class FakeRuntimeEnvironment : IRuntimeEnvironment
    {
        public bool IsWindows => true;
    }

    private sealed class RecordingRuntimeCommandExecutor : IRuntimeCommandExecutor
    {
        public List<RecordedCommand> Calls { get; } = [];

        public Task<RuntimeCommandResult> ExecuteAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
        {
            Calls.Add(new RecordedCommand(fileName, arguments.ToArray()));
            return Task.FromResult(new RuntimeCommandResult(0, string.Empty, string.Empty));
        }
    }

    private sealed record RecordedCommand(string FileName, string[] Arguments);
}
