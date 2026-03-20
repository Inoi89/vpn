using System.Net;
using VpnClient.Core.Interfaces;

namespace VpnClient.Infrastructure.Runtime;

public sealed class WindowsKillSwitchService : IKillSwitchService
{
    private const string AllowVpnRuleName = "YourVpnClient Kill Switch - Allow VPN";
    private const string AllowLocalRuleName = "YourVpnClient Kill Switch - Allow Local";
    private const string BlockAllRuleName = "YourVpnClient Kill Switch - Block All";

    private readonly IRuntimeCommandExecutor _commandExecutor;
    private readonly IRuntimeEnvironment _environment;
    private string? _armedEndpointSignature;

    public WindowsKillSwitchService(
        IRuntimeCommandExecutor commandExecutor,
        IRuntimeEnvironment environment)
    {
        _commandExecutor = commandExecutor;
        _environment = environment;
    }

    public bool IsArmed => !string.IsNullOrWhiteSpace(_armedEndpointSignature);

    public async Task ArmAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        if (!_environment.IsWindows)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException("Kill switch cannot be armed without a VPN endpoint.");
        }

        var parsed = ParseEndpoint(endpoint);
        if (string.Equals(_armedEndpointSignature, parsed.Signature, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await DisarmAsync(cancellationToken);

        await ExecuteOrThrowAsync(
            "netsh",
            [
                "advfirewall", "firewall", "add", "rule",
                $"name={AllowVpnRuleName}",
                "dir=out",
                "action=allow",
                "profile=any",
                "protocol=UDP",
                $"remoteport={parsed.Port}",
                $"remoteip={parsed.RemoteIpExpression}"
            ],
            cancellationToken);

        await ExecuteOrThrowAsync(
            "netsh",
            [
                "advfirewall", "firewall", "add", "rule",
                $"name={AllowLocalRuleName}",
                "dir=out",
                "action=allow",
                "profile=any",
                "protocol=any",
                "remoteip=127.0.0.1,::1,LocalSubnet"
            ],
            cancellationToken);

        await ExecuteOrThrowAsync(
            "netsh",
            [
                "advfirewall", "firewall", "add", "rule",
                $"name={BlockAllRuleName}",
                "dir=out",
                "action=block",
                "profile=any",
                "protocol=any",
                "remoteip=any"
            ],
            cancellationToken);

        _armedEndpointSignature = parsed.Signature;
    }

    public async Task DisarmAsync(CancellationToken cancellationToken = default)
    {
        if (!_environment.IsWindows)
        {
            return;
        }

        await DeleteRuleIfPresentAsync(AllowVpnRuleName, cancellationToken);
        await DeleteRuleIfPresentAsync(AllowLocalRuleName, cancellationToken);
        await DeleteRuleIfPresentAsync(BlockAllRuleName, cancellationToken);
        _armedEndpointSignature = null;
    }

    private async Task DeleteRuleIfPresentAsync(string ruleName, CancellationToken cancellationToken)
    {
        await _commandExecutor.ExecuteAsync(
            "netsh",
            ["advfirewall", "firewall", "delete", "rule", $"name={ruleName}"],
            cancellationToken);
    }

    private async Task ExecuteOrThrowAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var result = await _commandExecutor.ExecuteAsync(fileName, arguments, cancellationToken);
        if (result.ExitCode == 0)
        {
            return;
        }

        var error = !string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardError.Trim()
            : !string.IsNullOrWhiteSpace(result.StandardOutput)
                ? result.StandardOutput.Trim()
                : "Unknown firewall error.";

        throw new InvalidOperationException(error);
    }

    private static ParsedEndpoint ParseEndpoint(string endpoint)
    {
        if (endpoint.StartsWith("[", StringComparison.Ordinal))
        {
            var closingBracketIndex = endpoint.IndexOf(']');
            if (closingBracketIndex <= 0 || closingBracketIndex + 2 >= endpoint.Length)
            {
                throw new InvalidOperationException($"Endpoint '{endpoint}' is not in a supported format.");
            }

            var host = endpoint[1..closingBracketIndex];
            var port = endpoint[(closingBracketIndex + 2)..];
            return new ParsedEndpoint($"{host}:{port}", port, host);
        }

        var separatorIndex = endpoint.LastIndexOf(':');
        if (separatorIndex <= 0 || separatorIndex >= endpoint.Length - 1)
        {
            throw new InvalidOperationException($"Endpoint '{endpoint}' is not in a supported format.");
        }

        var rawHost = endpoint[..separatorIndex];
        var rawPort = endpoint[(separatorIndex + 1)..];
        var remoteIpExpression = IPAddress.TryParse(rawHost, out _)
            ? rawHost
            : "any";

        return new ParsedEndpoint($"{rawHost}:{rawPort}", rawPort, remoteIpExpression);
    }

    private sealed record ParsedEndpoint(string Signature, string Port, string RemoteIpExpression);
}
