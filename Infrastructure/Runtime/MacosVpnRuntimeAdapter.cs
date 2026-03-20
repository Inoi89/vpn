using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
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
            await _transport.SendAsync(BuildActivationPayload(profile), cancellationToken);
            await _killSwitch.ArmAsync(profile.Endpoint ?? string.Empty, cancellationToken);
            _activeProfile = profile;
            return UpdateState(connectingState with
            {
                Status = RuntimeConnectionStatus.Connected,
                AdapterPresent = true,
                TunnelActive = true,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
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
            await _transport.SendAsync(new JsonObject
            {
                ["type"] = "deactivate"
            }, cancellationToken);
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
            using var response = await _transport.RequestAsync(new JsonObject
            {
                ["type"] = "status"
            }, cancellationToken);

            return UpdateState(ParseStatus(response.RootElement));
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
            using var response = await _transport.RequestAsync(new JsonObject
            {
                ["type"] = "status"
            }, cancellationToken);

            var state = ParseStatus(response.RootElement);
            if (state.Status is RuntimeConnectionStatus.Connected or RuntimeConnectionStatus.Connecting or RuntimeConnectionStatus.Degraded)
            {
                _activeProfile = profiles.FirstOrDefault();
            }

            return UpdateState(state);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "macOS runtime bridge restore probe failed.");
            return UpdateState(ConnectionState.Disconnected(AdapterName));
        }
    }

    private static JsonObject BuildActivationPayload(ImportedServerProfile profile)
    {
        var tunnel = profile.TunnelConfig;

        return new JsonObject
        {
            ["type"] = "activate",
            ["profile"] = new JsonObject
            {
                ["id"] = profile.Id.ToString(),
                ["name"] = profile.DisplayName,
                ["endpoint"] = profile.Endpoint,
                ["address"] = profile.Address,
                ["dns"] = new JsonArray(tunnel.DnsServers.Select(server => JsonValue.Create(server)).ToArray()),
                ["mtu"] = tunnel.Mtu is null ? null : JsonValue.Create(tunnel.Mtu),
                ["allowedIps"] = new JsonArray(tunnel.AllowedIps.Select(ip => JsonValue.Create(ip)).ToArray())
            }
        };
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

    private static ConnectionState ParseStatus(JsonElement root)
    {
        var connected = root.TryGetProperty("connected", out var connectedElement)
                        && connectedElement.ValueKind == JsonValueKind.True;

        var endpoint = TryGetString(root, "serverEndpoint")
                       ?? TryGetString(root, "serverIpv4Gateway")
                       ?? TryGetString(root, "endpoint");

        var address = TryGetString(root, "deviceIpv4Address")
                      ?? TryGetString(root, "deviceIpv6Address")
                      ?? TryGetString(root, "address");

        var received = TryGetInt64(root, "rxBytes");
        var sent = TryGetInt64(root, "txBytes");
        var handshake = TryGetDateTimeOffset(root, "latestHandshakeAtUtc")
                        ?? TryGetDateTimeOffset(root, "date");

        return new ConnectionState
        {
            Status = connected ? RuntimeConnectionStatus.Connected : RuntimeConnectionStatus.Disconnected,
            AdapterName = AdapterName,
            Endpoint = endpoint,
            Address = address,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            LatestHandshakeAtUtc = handshake,
            ReceivedBytes = received ?? 0,
            SentBytes = sent ?? 0,
            AdapterPresent = connected,
            TunnelActive = connected,
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

    private ConnectionState UpdateState(ConnectionState state)
    {
        _currentState = state;
        StateChanged?.Invoke(state);
        return state;
    }
}
