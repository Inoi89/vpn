using System.Globalization;
using Microsoft.Extensions.Logging;
using VpnClient.Core.Interfaces;
using VpnClient.Core.Models;

namespace VpnClient.Infrastructure.Runtime;

public sealed class WindowsFirstVpnRuntimeAdapter : IVpnRuntimeAdapter
{
    private const string AdapterName = "VpnClient";

    private readonly IWintunService _wintun;
    private readonly IRuntimeCommandExecutor _commandExecutor;
    private readonly IRuntimeEnvironment _environment;
    private readonly IWindowsRuntimeAssetLocator _assetLocator;
    private readonly ILogger<WindowsFirstVpnRuntimeAdapter> _logger;
    private readonly string _wgExecutablePath;
    private ConnectionState _currentState = ConnectionState.Disconnected(AdapterName);

    public WindowsFirstVpnRuntimeAdapter(
        IWintunService wintun,
        IRuntimeCommandExecutor commandExecutor,
        IRuntimeEnvironment environment,
        IWindowsRuntimeAssetLocator assetLocator,
        ILogger<WindowsFirstVpnRuntimeAdapter> logger)
    {
        _wintun = wintun;
        _commandExecutor = commandExecutor;
        _environment = environment;
        _assetLocator = assetLocator;
        _logger = logger;
        _wgExecutablePath = assetLocator.WgExecutablePath;
    }

    public ConnectionState CurrentState => _currentState;

    public event Action<ConnectionState>? StateChanged;

    public async Task<ConnectionState> ConnectAsync(ImportedServerProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (!_environment.IsWindows)
        {
            return UpdateState(ConnectionState.Unsupported(AdapterName, "Windows fallback runtime is supported only on Windows."));
        }

        if (_currentState.Status is RuntimeConnectionStatus.Connecting or RuntimeConnectionStatus.Disconnecting)
        {
            return _currentState;
        }

        var runtimeProfile = RuntimeWireGuardProfile.FromProfile(profile, _assetLocator.GetWarnings());
        if (runtimeProfile.ValidationError is not null)
        {
            return UpdateState(BuildState(
                RuntimeConnectionStatus.Failed,
                runtimeProfile,
                runtimeProfile.Warnings,
                adapterPresent: false,
                tunnelActive: false,
                lastError: runtimeProfile.ValidationError));
        }

        var connectingState = BuildState(
            RuntimeConnectionStatus.Connecting,
            runtimeProfile,
            runtimeProfile.Warnings,
            adapterPresent: false,
            tunnelActive: false,
            lastError: null);
        UpdateState(connectingState);

        var privateKeyPath = Path.GetTempFileName();
        var pskPath = string.IsNullOrWhiteSpace(runtimeProfile.PresharedKey) ? null : Path.GetTempFileName();

        try
        {
            await File.WriteAllTextAsync(privateKeyPath, runtimeProfile.PrivateKey!, cancellationToken);
            if (pskPath is not null)
            {
                await File.WriteAllTextAsync(pskPath, runtimeProfile.PresharedKey!, cancellationToken);
            }

            await _wintun.CreateAdapterAsync(AdapterName);

            var commandWarnings = new List<string>();
            await ConfigureAddressAsync(runtimeProfile, cancellationToken, commandWarnings);
            await ConfigureDnsAsync(runtimeProfile, cancellationToken, commandWarnings);
            await ConfigureMtuAsync(runtimeProfile, cancellationToken, commandWarnings);
            await ConfigurePeerAsync(runtimeProfile, privateKeyPath, pskPath, cancellationToken);
            await ConfigureRoutesAsync(runtimeProfile, cancellationToken, commandWarnings);

            return UpdateState(BuildState(
                RuntimeConnectionStatus.Connected,
                runtimeProfile,
                runtimeProfile.Warnings.Concat(commandWarnings).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                adapterPresent: true,
                tunnelActive: false,
                lastError: null));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Fallback Windows runtime failed to connect.");

            try
            {
                await _wintun.DeleteAdapterAsync(AdapterName);
            }
            catch (Exception cleanupException)
            {
                _logger.LogWarning(cleanupException, "Failed to clean up adapter after connect failure.");
            }

            return UpdateState(BuildState(
                RuntimeConnectionStatus.Failed,
                runtimeProfile,
                runtimeProfile.Warnings,
                adapterPresent: false,
                tunnelActive: false,
                lastError: exception.Message));
        }
        finally
        {
            TryDeleteFile(privateKeyPath);
            if (pskPath is not null)
            {
                TryDeleteFile(pskPath);
            }
        }
    }

    public async Task<ConnectionState> DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (!_environment.IsWindows)
        {
            return UpdateState(ConnectionState.Unsupported(AdapterName, "Windows fallback runtime is supported only on Windows."));
        }

        if (_currentState.Status is RuntimeConnectionStatus.Disconnected or RuntimeConnectionStatus.Disconnecting)
        {
            return _currentState;
        }

        UpdateState(_currentState with
        {
            Status = RuntimeConnectionStatus.Disconnecting,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });

        try
        {
            await _wintun.DeleteAdapterAsync(AdapterName);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Adapter deletion reported an error.");
            return UpdateState(_currentState with
            {
                Status = RuntimeConnectionStatus.Degraded,
                LastError = exception.Message,
                Warnings = _currentState.Warnings.Concat(new[] { $"Adapter deletion reported an error: {exception.Message}" }).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                AdapterPresent = false,
                TunnelActive = false,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
        }

        return UpdateState(ConnectionState.Disconnected(AdapterName));
    }

    public async Task<ConnectionState> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        if (!_environment.IsWindows)
        {
            return UpdateState(ConnectionState.Unsupported(AdapterName, "Windows fallback runtime is supported only on Windows."));
        }

        if (_currentState.Status == RuntimeConnectionStatus.Disconnected)
        {
            return _currentState with
            {
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };
        }

        var probe = await _commandExecutor.ExecuteAsync(_wgExecutablePath, ["show", AdapterName, "dump"], cancellationToken);
        if (probe.ExitCode != 0)
        {
            return UpdateState(_currentState with
            {
                Status = RuntimeConnectionStatus.Disconnected,
                AdapterPresent = false,
                TunnelActive = false,
                LastError = string.IsNullOrWhiteSpace(probe.StandardError) ? "wg.exe show failed." : probe.StandardError.Trim(),
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
        }

        var runtime = RuntimeWireGuardDump.Parse(probe.StandardOutput, AdapterName);
        var mergedWarnings = _currentState.Warnings.Concat(runtime.Warnings).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        return UpdateState(_currentState with
        {
            Status = runtime.IsTunnelActive
                ? RuntimeConnectionStatus.Connected
                : RuntimeConnectionStatus.Degraded,
            AdapterPresent = true,
            TunnelActive = runtime.IsTunnelActive,
            Endpoint = runtime.Endpoint ?? _currentState.Endpoint,
            LatestHandshakeAtUtc = runtime.LatestHandshakeAtUtc,
            ReceivedBytes = runtime.ReceivedBytes,
            SentBytes = runtime.SentBytes,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            Warnings = mergedWarnings
        });
    }

    public async Task<ConnectionState> TryRestoreAsync(IReadOnlyList<ImportedServerProfile> profiles, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profiles);

        if (!_environment.IsWindows)
        {
            return UpdateState(ConnectionState.Unsupported(AdapterName, "Windows fallback runtime is supported only on Windows."));
        }

        if (_currentState.Status != RuntimeConnectionStatus.Disconnected)
        {
            return await GetStatusAsync(cancellationToken);
        }

        var probe = await _commandExecutor.ExecuteAsync(_wgExecutablePath, ["show", AdapterName, "dump"], cancellationToken);
        if (probe.ExitCode != 0)
        {
            return UpdateState(ConnectionState.Disconnected(AdapterName));
        }

        var runtime = RuntimeWireGuardDump.Parse(probe.StandardOutput, AdapterName);
        var matchedProfile = TryMatchProfile(runtime, profiles);
        if (matchedProfile is null)
        {
            return UpdateState(new ConnectionState
            {
                Status = runtime.IsTunnelActive
                    ? RuntimeConnectionStatus.Connected
                    : RuntimeConnectionStatus.Degraded,
                AdapterName = AdapterName,
                Endpoint = runtime.Endpoint,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                LatestHandshakeAtUtc = runtime.LatestHandshakeAtUtc,
                ReceivedBytes = runtime.ReceivedBytes,
                SentBytes = runtime.SentBytes,
                AdapterPresent = true,
                TunnelActive = runtime.IsTunnelActive,
                IsWindowsFirst = true,
                UsesSetConf = false,
                Warnings = runtime.Warnings.Concat(new[]
                {
                    "A fallback WireGuard tunnel is active, but it could not be mapped to a local imported profile."
                }).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            });
        }

        var runtimeProfile = RuntimeWireGuardProfile.FromProfile(matchedProfile, _assetLocator.GetWarnings());
        return UpdateState(BuildState(
            runtime.IsTunnelActive
                ? RuntimeConnectionStatus.Connected
                : RuntimeConnectionStatus.Degraded,
            runtimeProfile,
            runtimeProfile.Warnings.Concat(runtime.Warnings).Concat(new[]
            {
                "Restored an existing fallback tunnel state from the local adapter."
            }).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            adapterPresent: true,
            tunnelActive: runtime.IsTunnelActive,
            lastError: null) with
        {
            Endpoint = runtime.Endpoint ?? runtimeProfile.Endpoint,
            LatestHandshakeAtUtc = runtime.LatestHandshakeAtUtc,
            ReceivedBytes = runtime.ReceivedBytes,
            SentBytes = runtime.SentBytes,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
    }

    private async Task ConfigurePeerAsync(
        RuntimeWireGuardProfile profile,
        string privateKeyPath,
        string? pskPath,
        CancellationToken cancellationToken)
    {
        var arguments = new List<string>
        {
            "set",
            AdapterName,
            "private-key",
            privateKeyPath,
            "peer",
            profile.ServerPublicKey!
        };

        if (!string.IsNullOrWhiteSpace(pskPath))
        {
            arguments.Add("preshared-key");
            arguments.Add(pskPath);
        }

        arguments.Add("endpoint");
        arguments.Add(profile.Endpoint!);

        if (profile.AllowedIps.Count > 0)
        {
            arguments.Add("allowed-ips");
            arguments.Add(string.Join(", ", profile.AllowedIps));
        }

        arguments.Add("persistent-keepalive");
        arguments.Add((profile.PersistentKeepalive ?? 25).ToString(CultureInfo.InvariantCulture));

        var result = await _commandExecutor.ExecuteAsync(_wgExecutablePath, arguments, cancellationToken);
        EnsureCommandSucceeded(result, $"{Path.GetFileName(_wgExecutablePath)} set failed.");
    }

    private async Task ConfigureAddressAsync(
        RuntimeWireGuardProfile profile,
        CancellationToken cancellationToken,
        ICollection<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(profile.Address))
        {
            warnings.Add("Address is missing. The fallback runtime could not configure the adapter IP.");
            return;
        }

        var parts = profile.Address.Split('/', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            warnings.Add($"Address '{profile.Address}' is not in CIDR format.");
            return;
        }

        if (parts[0].Contains('.'))
        {
            if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var prefix))
            {
                warnings.Add($"IPv4 prefix '{parts[1]}' could not be parsed.");
                return;
            }

            var mask = PrefixToMask(prefix);
            var result = await _commandExecutor.ExecuteAsync(
                "netsh",
                ["interface", "ip", "set", "address", $"name={AdapterName}", "static", parts[0], mask],
                cancellationToken);
            EnsureCommandSucceeded(result, $"Failed to set IPv4 address {profile.Address}.");
            return;
        }

        var ipv6Result = await _commandExecutor.ExecuteAsync(
            "netsh",
            ["interface", "ipv6", "add", "address", AdapterName, profile.Address],
            cancellationToken);
        EnsureCommandSucceeded(ipv6Result, $"Failed to set IPv6 address {profile.Address}.");
    }

    private async Task ConfigureDnsAsync(
        RuntimeWireGuardProfile profile,
        CancellationToken cancellationToken,
        ICollection<string> warnings)
    {
        if (profile.DnsServers.Count == 0)
        {
            warnings.Add("DNS servers are missing. The fallback runtime will not override resolvers.");
            return;
        }

        var primary = profile.DnsServers[0];
        var setResult = await _commandExecutor.ExecuteAsync(
            "netsh",
            ["interface", "ip", "set", "dns", $"name={AdapterName}", "static", primary],
            cancellationToken);
        EnsureCommandSucceeded(setResult, $"Failed to set DNS server '{primary}'.");

        for (var index = 1; index < profile.DnsServers.Count; index++)
        {
            var dns = profile.DnsServers[index];
            var addResult = await _commandExecutor.ExecuteAsync(
                "netsh",
                ["interface", "ip", "add", "dns", $"name={AdapterName}", dns, $"index={index + 1}"],
                cancellationToken);
            EnsureCommandSucceeded(addResult, $"Failed to add secondary DNS server '{dns}'.");
        }
    }

    private async Task ConfigureMtuAsync(
        RuntimeWireGuardProfile profile,
        CancellationToken cancellationToken,
        ICollection<string> warnings)
    {
        if (profile.Mtu is null)
        {
            warnings.Add("MTU was not provided. The fallback runtime will keep the OS default.");
            return;
        }

        var mtu = profile.Mtu.Value.ToString(CultureInfo.InvariantCulture);
        foreach (var family in new[] { "ipv4", "ipv6" })
        {
            var result = await _commandExecutor.ExecuteAsync(
                "netsh",
                ["interface", family, "set", "subinterface", AdapterName, $"mtu={mtu}", "store=active"],
                cancellationToken);
            EnsureCommandSucceeded(result, $"Failed to set {family} MTU to {mtu}.");
        }
    }

    private async Task ConfigureRoutesAsync(
        RuntimeWireGuardProfile profile,
        CancellationToken cancellationToken,
        ICollection<string> warnings)
    {
        if (profile.AllowedIps.Count == 0)
        {
            warnings.Add("AllowedIPs are missing. The fallback runtime will not add OS routes.");
            return;
        }

        foreach (var allowedIp in profile.AllowedIps)
        {
            var parts = allowedIp.Split('/', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
            {
                warnings.Add($"Allowed IP '{allowedIp}' is not in CIDR format and was skipped.");
                continue;
            }

            RuntimeCommandResult result;
            if (parts[0].Contains('.'))
            {
                if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var prefix))
                {
                    warnings.Add($"Allowed IP prefix '{parts[1]}' could not be parsed for '{allowedIp}'.");
                    continue;
                }

                result = await _commandExecutor.ExecuteAsync(
                    "netsh",
                    ["interface", "ipv4", "add", "route", parts[0], PrefixToMask(prefix), AdapterName],
                    cancellationToken);
            }
            else
            {
                result = await _commandExecutor.ExecuteAsync(
                    "netsh",
                    ["interface", "ipv6", "add", "route", allowedIp, $"interface={AdapterName}"],
                    cancellationToken);
            }

            if (result.ExitCode == 0)
            {
                continue;
            }

            var error = string.IsNullOrWhiteSpace(result.StandardError) ? "Unknown route error." : result.StandardError.Trim();
            if (LooksLikeDuplicateRoute(error))
            {
                warnings.Add(error);
                continue;
            }

            throw new InvalidOperationException($"Failed to add route for '{allowedIp}': {error}");
        }
    }

    private static bool LooksLikeDuplicateRoute(string error)
    {
        return error.Contains("already exists", StringComparison.OrdinalIgnoreCase)
               || error.Contains("object already exists", StringComparison.OrdinalIgnoreCase)
               || error.Contains("element not found", StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureCommandSucceeded(RuntimeCommandResult result, string message)
    {
        if (result.ExitCode == 0)
        {
            return;
        }

        throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.StandardError)
            ? message
            : $"{message} {result.StandardError.Trim()}");
    }

    private ConnectionState BuildState(
        RuntimeConnectionStatus status,
        RuntimeWireGuardProfile profile,
        IReadOnlyCollection<string> warnings,
        bool adapterPresent,
        bool tunnelActive,
        string? lastError)
    {
        return new ConnectionState
        {
            Status = warnings.Count > 0 && status == RuntimeConnectionStatus.Connected
                ? RuntimeConnectionStatus.Degraded
                : status,
            AdapterName = AdapterName,
            ProfileId = profile.ProfileId,
            ProfileName = profile.ProfileName,
            Endpoint = profile.Endpoint,
            Address = profile.Address,
            DnsServers = profile.DnsServers,
            Mtu = profile.Mtu,
            AllowedIps = profile.AllowedIps,
            Routes = profile.AllowedIps,
            Warnings = warnings.ToArray(),
            LastError = lastError,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            LatestHandshakeAtUtc = null,
            ReceivedBytes = 0,
            SentBytes = 0,
            AdapterPresent = adapterPresent,
            TunnelActive = tunnelActive,
            IsWindowsFirst = true,
            UsesSetConf = false
        };
    }

    private ConnectionState UpdateState(ConnectionState state)
    {
        _currentState = state;
        StateChanged?.Invoke(state);
        return state;
    }

    private static string PrefixToMask(int prefixLength)
    {
        if (prefixLength <= 0)
        {
            return "0.0.0.0";
        }

        var mask = uint.MaxValue << (32 - prefixLength);
        var bytes = BitConverter.GetBytes(mask);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }

        return string.Join('.', bytes);
    }

    private static ImportedServerProfile? TryMatchProfile(RuntimeWireGuardDump runtime, IReadOnlyList<ImportedServerProfile> profiles)
    {
        var publicKeyMatches = profiles
            .Where(profile => string.Equals(
                profile.TunnelConfig.PublicKey ?? TryGet(profile.TunnelConfig.PeerValues, "PublicKey"),
                runtime.PeerPublicKey,
                StringComparison.Ordinal))
            .ToArray();

        if (publicKeyMatches.Length == 1)
        {
            return publicKeyMatches[0];
        }

        var endpointMatches = profiles
            .Where(profile => string.Equals(profile.Endpoint, runtime.Endpoint, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return endpointMatches.Length == 1 ? endpointMatches[0] : null;
    }

    private static string? TryGet(IReadOnlyDictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out var value) ? value : null;
    }

    private void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "Failed to delete temporary file {Path}.", path);
        }
    }

    private sealed record RuntimeWireGuardProfile(
        Guid ProfileId,
        string ProfileName,
        string? PrivateKey,
        string? Address,
        IReadOnlyList<string> DnsServers,
        int? Mtu,
        IReadOnlyList<string> AllowedIps,
        string? ServerPublicKey,
        string? PresharedKey,
        string? Endpoint,
        int? PersistentKeepalive,
        IReadOnlyList<string> Warnings,
        string? ValidationError)
    {
        public static RuntimeWireGuardProfile FromProfile(
            ImportedServerProfile profile,
            IEnumerable<string>? runtimeWarnings = null)
        {
            var config = profile.TunnelConfig;
            var warnings = runtimeWarnings?
                .Where(warning => !string.IsNullOrWhiteSpace(warning))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? [];

            if (config.AwgValues.Count > 0)
            {
                warnings.Add("AWG-specific runtime keys were detected. The fallback runtime preserves them in profile state but cannot reproduce the full Amnezia daemon path.");
            }

            var privateKey = TryGet(config.InterfaceValues, "PrivateKey");
            var publicKey = config.PublicKey ?? TryGet(config.PeerValues, "PublicKey");
            var presharedKey = config.PresharedKey ?? TryGet(config.PeerValues, "PreSharedKey");
            var keepaliveValue = TryGet(config.PeerValues, "PersistentKeepalive");
            int? mtu = int.TryParse(config.Mtu, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedMtu)
                ? parsedMtu
                : null;
            int? keepalive = int.TryParse(keepaliveValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedKeepalive)
                ? parsedKeepalive
                : null;

            var validationError = Validate(privateKey, publicKey, config.Endpoint, config.Address);

            return new RuntimeWireGuardProfile(
                profile.Id,
                profile.DisplayName,
                privateKey,
                config.Address,
                config.DnsServers.ToArray(),
                mtu,
                config.AllowedIps.ToArray(),
                publicKey,
                presharedKey,
                config.Endpoint,
                keepalive,
                warnings,
                validationError);
        }

        private static string? Validate(string? privateKey, string? publicKey, string? endpoint, string? address)
        {
            if (string.IsNullOrWhiteSpace(privateKey))
            {
                return "PrivateKey is required.";
            }

            if (string.IsNullOrWhiteSpace(publicKey))
            {
                return "PublicKey is required.";
            }

            if (string.IsNullOrWhiteSpace(endpoint))
            {
                return "Endpoint is required.";
            }

            if (string.IsNullOrWhiteSpace(address))
            {
                return "Address is required.";
            }

            return null;
        }

        private static string? TryGet(IReadOnlyDictionary<string, string> values, string key)
        {
            return values.TryGetValue(key, out var value) ? value : null;
        }
    }

}
