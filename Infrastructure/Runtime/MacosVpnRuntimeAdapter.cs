using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using VpnClient.Core.Interfaces;
using VpnClient.Core.Models;

namespace VpnClient.Infrastructure.Runtime;

public sealed class MacosVpnRuntimeAdapter : IVpnRuntimeAdapter
{
    private const string AdapterName = "MacOSRuntime";

    private readonly IMacosRuntimeBridgeTransport _transport;
    private readonly IKillSwitchService _killSwitch;
    private readonly IRuntimeEnvironment _environment;
    private readonly ILogger<MacosVpnRuntimeAdapter> _logger;
    private ImportedServerProfile? _activeProfile;
    private ConnectionState _currentState = ConnectionState.Disconnected(AdapterName);

    public MacosVpnRuntimeAdapter(
        IMacosRuntimeBridgeTransport transport,
        IKillSwitchService killSwitch,
        IRuntimeEnvironment environment,
        ILogger<MacosVpnRuntimeAdapter> logger)
    {
        _transport = transport;
        _killSwitch = killSwitch;
        _environment = environment;
        _logger = logger;
    }

    public ConnectionState CurrentState => _currentState;

    public event Action<ConnectionState>? StateChanged;

    public async Task<ConnectionState> ConnectAsync(ImportedServerProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (!_environment.IsMacOS)
        {
            return UpdateState(ConnectionState.Unsupported(AdapterName, "macOS runtime bridge is supported only on macOS."));
        }

        if (!await _transport.IsAvailableAsync(cancellationToken))
        {
            return UpdateState(ConnectionState.Unsupported(AdapterName, "macOS runtime bridge is not available. Start the helper service first."));
        }

        var warnings = BuildWarnings(profile);
        var connectingState = BuildState(profile, RuntimeConnectionStatus.Connecting, warnings);
        UpdateState(connectingState);

        try
        {
            using (var hello = await _transport.RequestAsync(MacosRuntimeBridgeProtocol.BuildHelloRequest(), cancellationToken))
            {
                MacosRuntimeBridgeProtocol.EnsureSuccess(hello);
            }

            using (var configure = await _transport.RequestAsync(MacosRuntimeBridgeProtocol.BuildConfigureRequest(profile), cancellationToken))
            {
                MacosRuntimeBridgeProtocol.EnsureSuccess(configure);
            }

            using (var activate = await _transport.RequestAsync(MacosRuntimeBridgeProtocol.BuildActivateRequest(profile), cancellationToken))
            {
                MacosRuntimeBridgeProtocol.EnsureSuccess(activate);
            }

            await _killSwitch.ArmAsync(profile.Endpoint ?? string.Empty, cancellationToken);
            _activeProfile = profile;
            return await GetStatusAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "macOS runtime bridge connect failed.");
            return UpdateState(connectingState with
            {
                Status = RuntimeConnectionStatus.Failed,
                LastError = exception.Message,
                Warnings = warnings.Concat(new[] { exception.Message }).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
        }
    }

    public async Task<ConnectionState> DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (!_environment.IsMacOS)
        {
            return UpdateState(ConnectionState.Unsupported(AdapterName, "macOS runtime bridge is supported only on macOS."));
        }

        if (!await _transport.IsAvailableAsync(cancellationToken))
        {
            _activeProfile = null;
            await _killSwitch.DisarmAsync(cancellationToken);
            return UpdateState(ConnectionState.Disconnected(AdapterName));
        }

        try
        {
            using var response = await _transport.RequestAsync(
                MacosRuntimeBridgeProtocol.BuildDeactivateRequest(_activeProfile?.Id),
                cancellationToken);
            MacosRuntimeBridgeProtocol.EnsureSuccess(response);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "macOS runtime bridge deactivate failed.");
        }

        await _killSwitch.DisarmAsync(cancellationToken);
        _activeProfile = null;
        return UpdateState(ConnectionState.Disconnected(AdapterName));
    }

    public async Task<ConnectionState> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        if (!_environment.IsMacOS)
        {
            return UpdateState(ConnectionState.Unsupported(AdapterName, "macOS runtime bridge is supported only on macOS."));
        }

        if (!await _transport.IsAvailableAsync(cancellationToken))
        {
            return UpdateState(ConnectionState.Unsupported(AdapterName, "macOS runtime bridge is not available. Start the helper service first."));
        }

        try
        {
            using var response = await _transport.RequestAsync(MacosRuntimeBridgeProtocol.BuildStatusRequest(), cancellationToken);
            var payload = MacosRuntimeBridgeProtocol.ExtractPayloadOrRoot(response);
            return UpdateState(ParseStatus(payload));
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "macOS runtime bridge status probe failed.");
            return UpdateState(_currentState with
            {
                Status = RuntimeConnectionStatus.Degraded,
                LastError = exception.Message,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
        }
    }

    public async Task<ConnectionState> TryRestoreAsync(IReadOnlyList<ImportedServerProfile> profiles, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profiles);

        if (!_environment.IsMacOS)
        {
            return UpdateState(ConnectionState.Unsupported(AdapterName, "macOS runtime bridge is supported only on macOS."));
        }

        if (!await _transport.IsAvailableAsync(cancellationToken))
        {
            return UpdateState(ConnectionState.Unsupported(AdapterName, "macOS runtime bridge is not available. Start the helper service first."));
        }

        if (_activeProfile is not null)
        {
            return await GetStatusAsync(cancellationToken);
        }

        try
        {
            using var response = await _transport.RequestAsync(MacosRuntimeBridgeProtocol.BuildStatusRequest(), cancellationToken);
            var payload = MacosRuntimeBridgeProtocol.ExtractPayloadOrRoot(response);
            var state = ParseStatus(payload);
            if (state.Status is RuntimeConnectionStatus.Connected or RuntimeConnectionStatus.Connecting or RuntimeConnectionStatus.Degraded)
            {
                _activeProfile = TryMatchProfile(payload, profiles) ?? profiles.FirstOrDefault();
            }

            return UpdateState(state);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "macOS runtime bridge restore probe failed.");
            return UpdateState(ConnectionState.Disconnected(AdapterName));
        }
    }

    private static ConnectionState BuildState(
        ImportedServerProfile profile,
        RuntimeConnectionStatus status,
        IReadOnlyList<string> warnings)
    {
        return new ConnectionState
        {
            Status = status,
            AdapterName = AdapterName,
            ProfileId = profile.Id,
            ProfileName = profile.DisplayName,
            Endpoint = profile.Endpoint,
            Address = profile.Address,
            DnsServers = profile.TunnelConfig.DnsServers,
            Mtu = ParseNullableInt(profile.TunnelConfig.Mtu),
            AllowedIps = profile.TunnelConfig.AllowedIps,
            Routes = profile.TunnelConfig.AllowedIps,
            Warnings = warnings,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            AdapterPresent = true,
            TunnelActive = status is RuntimeConnectionStatus.Connected,
            IsWindowsFirst = false,
            UsesSetConf = false
        };
    }

    private static IReadOnlyList<string> BuildWarnings(ImportedServerProfile profile)
    {
        var warnings = new List<string>();

        if (profile.SourceFormat == TunnelConfigFormat.AmneziaVpn)
        {
            warnings.Add("macOS runtime bridge will materialize Amnezia package data before activation.");
        }

        return warnings;
    }

    private ConnectionState ParseStatus(JsonElement root)
    {
        var status = ParseRuntimeStatus(root);
        var connected = status is RuntimeConnectionStatus.Connected or RuntimeConnectionStatus.Degraded;
        var fallbackProfile = _activeProfile;

        var endpoint = TryGetString(root, "serverEndpoint")
                       ?? TryGetString(root, "serverIpv4Gateway")
                       ?? TryGetString(root, "endpoint")
                       ?? fallbackProfile?.Endpoint;

        var address = TryGetString(root, "deviceIpv4Address")
                      ?? TryGetString(root, "deviceIpv6Address")
                      ?? TryGetString(root, "address")
                      ?? fallbackProfile?.Address;

        var received = TryGetInt64(root, "rxBytes");
        var sent = TryGetInt64(root, "txBytes");
        var handshake = TryGetDateTimeOffset(root, "latestHandshakeAtUtc")
                        ?? TryGetDateTimeOffset(root, "date");
        var warnings = TryGetStringArray(root, "warnings");
        var lastError = TryGetString(root, "lastError")
                        ?? MacosRuntimeBridgeProtocol.TryExtractError(root);
        var dnsServers = TryGetStringArray(root, "dns");
        var allowedIps = TryGetStringArray(root, "allowedIps");
        var routes = TryGetStringArray(root, "routes");

        return new ConnectionState
        {
            Status = status,
            AdapterName = AdapterName,
            ProfileId = TryGetGuid(root, "profileId") ?? fallbackProfile?.Id,
            ProfileName = TryGetString(root, "profileName") ?? fallbackProfile?.DisplayName,
            Endpoint = endpoint,
            Address = address,
            DnsServers = dnsServers.Count > 0 ? dnsServers : fallbackProfile?.DnsServers ?? Array.Empty<string>(),
            Mtu = TryGetInt(root, "mtu") ?? ParseNullableInt(fallbackProfile?.Mtu),
            AllowedIps = allowedIps.Count > 0 ? allowedIps : fallbackProfile?.AllowedIps ?? Array.Empty<string>(),
            Routes = routes.Count > 0 ? routes : allowedIps.Count > 0 ? allowedIps : fallbackProfile?.AllowedIps ?? Array.Empty<string>(),
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            LatestHandshakeAtUtc = handshake,
            ReceivedBytes = received ?? 0,
            SentBytes = sent ?? 0,
            AdapterPresent = status is not RuntimeConnectionStatus.Disconnected and not RuntimeConnectionStatus.Unsupported,
            TunnelActive = connected,
            Warnings = warnings,
            LastError = lastError,
            IsWindowsFirst = false,
            UsesSetConf = false
        };
    }

    private static string? TryGetString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;
    }

    private static long? TryGetInt64(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var element) && element.TryGetInt64(out var value)
            ? value
            : null;
    }

    private static int? TryGetInt(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var element) && element.TryGetInt32(out var value)
            ? value
            : null;
    }

    private static Guid? TryGetGuid(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var element)
               && element.ValueKind == JsonValueKind.String
               && Guid.TryParse(element.GetString(), out var value)
            ? value
            : null;
    }

    private static IReadOnlyList<string> TryGetStringArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element) || element.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return element.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToArray();
    }

    private static RuntimeConnectionStatus ParseRuntimeStatus(JsonElement root)
    {
        var explicitStatus = TryGetString(root, "status")
                             ?? TryGetString(root, "state");
        if (!string.IsNullOrWhiteSpace(explicitStatus)
            && Enum.TryParse<RuntimeConnectionStatus>(explicitStatus, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        var connected = root.TryGetProperty("connected", out var connectedElement)
                        && connectedElement.ValueKind == JsonValueKind.True;
        return connected
            ? RuntimeConnectionStatus.Connected
            : RuntimeConnectionStatus.Disconnected;
    }

    private static DateTimeOffset? TryGetDateTimeOffset(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var element) && element.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(element.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value)
            ? value
            : null;
    }

    private static int? ParseNullableInt(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static ImportedServerProfile? TryMatchProfile(JsonElement status, IReadOnlyList<ImportedServerProfile> profiles)
    {
        var profileId = TryGetGuid(status, "profileId");
        if (profileId is not null)
        {
            var byId = profiles.FirstOrDefault(profile => profile.Id == profileId.Value);
            if (byId is not null)
            {
                return byId;
            }
        }

        var profileName = TryGetString(status, "profileName");
        if (!string.IsNullOrWhiteSpace(profileName))
        {
            var byName = profiles.FirstOrDefault(profile => string.Equals(profile.DisplayName, profileName, StringComparison.OrdinalIgnoreCase));
            if (byName is not null)
            {
                return byName;
            }
        }

        var endpoint = TryGetString(status, "serverEndpoint") ?? TryGetString(status, "endpoint");
        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            var byEndpoint = profiles.FirstOrDefault(profile => string.Equals(profile.Endpoint, endpoint, StringComparison.OrdinalIgnoreCase));
            if (byEndpoint is not null)
            {
                return byEndpoint;
            }
        }

        var address = TryGetString(status, "deviceIpv4Address")
                      ?? TryGetString(status, "deviceIpv6Address")
                      ?? TryGetString(status, "address");
        return string.IsNullOrWhiteSpace(address)
            ? null
            : profiles.FirstOrDefault(profile => string.Equals(profile.Address, address, StringComparison.OrdinalIgnoreCase));
    }

    private ConnectionState UpdateState(ConnectionState state)
    {
        _currentState = state;
        StateChanged?.Invoke(state);
        return state;
    }
}
