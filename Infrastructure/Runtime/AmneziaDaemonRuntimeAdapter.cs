using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using VpnClient.Core.Interfaces;
using VpnClient.Core.Models;

namespace VpnClient.Infrastructure.Runtime;

public sealed class AmneziaDaemonRuntimeAdapter : IVpnRuntimeAdapter
{
    private const string AdapterName = "AmneziaDaemon";
    private const string DefaultIpv6Address = "fd58:baa6:dead::1";

    private readonly IAmneziaDaemonTransport _transport;
    private readonly IRuntimeEnvironment _environment;
    private readonly ILogger<AmneziaDaemonRuntimeAdapter> _logger;
    private ImportedServerProfile? _activeProfile;
    private ConnectionState _currentState = ConnectionState.Disconnected(AdapterName);

    public AmneziaDaemonRuntimeAdapter(
        IAmneziaDaemonTransport transport,
        IRuntimeEnvironment environment,
        ILogger<AmneziaDaemonRuntimeAdapter> logger)
    {
        _transport = transport;
        _environment = environment;
        _logger = logger;
    }

    public ConnectionState CurrentState => _currentState;

    public event Action<ConnectionState>? StateChanged;

    public async Task<ConnectionState> ConnectAsync(ImportedServerProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (!_environment.IsWindows)
        {
            return UpdateState(ConnectionState.Unsupported(AdapterName, "Amnezia daemon runtime is supported only on Windows."));
        }

        if (!await _transport.IsAvailableAsync(cancellationToken))
        {
            return UpdateState(ConnectionState.Unsupported(AdapterName, "Amnezia daemon is not available. Install or run the Amnezia runtime service first."));
        }

        var activation = BuildActivationPayload(profile);
        var warnings = BuildWarnings(profile).ToArray();

        _activeProfile = profile;
        UpdateState(new ConnectionState
        {
            Status = RuntimeConnectionStatus.Connecting,
            AdapterName = AdapterName,
            ProfileId = profile.Id,
            ProfileName = profile.DisplayName,
            Endpoint = profile.Endpoint,
            Address = profile.Address,
            DnsServers = profile.DnsServers,
            Mtu = ParseNullableInt(profile.Mtu),
            AllowedIps = profile.AllowedIps,
            Routes = profile.AllowedIps,
            Warnings = warnings,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            AdapterPresent = true,
            TunnelActive = false,
            IsWindowsFirst = false,
            UsesSetConf = false
        });

        try
        {
            await _transport.SendAsync(activation, cancellationToken);
            return await GetStatusAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Amnezia daemon activation failed.");
            return UpdateState(_currentState with
            {
                Status = RuntimeConnectionStatus.Failed,
                LastError = exception.Message,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
        }
    }

    public async Task<ConnectionState> DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (!_environment.IsWindows)
        {
            return UpdateState(ConnectionState.Unsupported(AdapterName, "Amnezia daemon runtime is supported only on Windows."));
        }

        if (!await _transport.IsAvailableAsync(cancellationToken))
        {
            _activeProfile = null;
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
            _logger.LogWarning(exception, "Amnezia daemon deactivate failed.");
            return UpdateState(_currentState with
            {
                Status = RuntimeConnectionStatus.Degraded,
                LastError = exception.Message,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
        }

        _activeProfile = null;
        return UpdateState(ConnectionState.Disconnected(AdapterName));
    }

    public async Task<ConnectionState> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        if (!_environment.IsWindows)
        {
            return UpdateState(ConnectionState.Unsupported(AdapterName, "Amnezia daemon runtime is supported only on Windows."));
        }

        if (!await _transport.IsAvailableAsync(cancellationToken))
        {
            return UpdateState(ConnectionState.Unsupported(AdapterName, "Amnezia daemon is not available. Install or run the Amnezia runtime service first."));
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
            _logger.LogWarning(exception, "Amnezia daemon status probe failed.");
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

        if (!_environment.IsWindows)
        {
            return UpdateState(ConnectionState.Unsupported(AdapterName, "Amnezia daemon runtime is supported only on Windows."));
        }

        if (!await _transport.IsAvailableAsync(cancellationToken))
        {
            return UpdateState(ConnectionState.Unsupported(AdapterName, "Amnezia daemon is not available. Install or run the Amnezia runtime service first."));
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

            var root = response.RootElement.Clone();
            var connected = root.TryGetProperty("connected", out var connectedElement)
                            && connectedElement.ValueKind == JsonValueKind.True;
            if (!connected)
            {
                return UpdateState(ConnectionState.Disconnected(AdapterName));
            }

            var matchedProfile = TryMatchProfile(root, profiles);
            if (matchedProfile is not null)
            {
                _activeProfile = matchedProfile;
                return UpdateState(ParseStatus(root) with
                {
                    Warnings = BuildWarnings(matchedProfile)
                        .Concat(new[]
                        {
                            "Restored an existing Amnezia daemon tunnel state from the local daemon."
                        })
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                });
            }

            return UpdateState(new ConnectionState
            {
                Status = RuntimeConnectionStatus.Connected,
                AdapterName = AdapterName,
                Endpoint = BuildDaemonEndpoint(root),
                Address = TryGetString(root, "deviceIpv4Address") ?? TryGetString(root, "deviceIpv6Address"),
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                AdapterPresent = true,
                TunnelActive = true,
                IsWindowsFirst = false,
                UsesSetConf = false,
                Warnings = new[]
                {
                    "An Amnezia daemon tunnel is active, but it could not be mapped to a local imported profile."
                }
            });
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Amnezia daemon restore probe failed.");
            return UpdateState(ConnectionState.Disconnected(AdapterName) with
            {
                Status = RuntimeConnectionStatus.Degraded,
                LastError = exception.Message,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
        }
    }

    private ConnectionState ParseStatus(JsonElement status)
    {
        var connected = status.TryGetProperty("connected", out var connectedElement)
                        && connectedElement.ValueKind == JsonValueKind.True;

        if (_activeProfile is null)
        {
            return connected
                ? _currentState with
                {
                    Status = RuntimeConnectionStatus.Connected,
                    TunnelActive = true,
                    AdapterPresent = true,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                }
                : ConnectionState.Disconnected(AdapterName);
        }

        var handshake = TryParseDaemonDate(status);
        var txBytes = TryGetInt64(status, "txBytes");
        var rxBytes = TryGetInt64(status, "rxBytes");
        var serverGateway = TryGetString(status, "serverIpv4Gateway");
        var deviceAddress = TryGetString(status, "deviceIpv4Address") ?? _activeProfile.Address;
        var hasTraffic = txBytes > 0 || rxBytes > 0;

        return new ConnectionState
        {
            Status = !connected
                ? RuntimeConnectionStatus.Connecting
                : handshake is not null || hasTraffic
                    ? RuntimeConnectionStatus.Connected
                    : RuntimeConnectionStatus.Connecting,
            AdapterName = AdapterName,
            ProfileId = _activeProfile.Id,
            ProfileName = _activeProfile.DisplayName,
            Endpoint = string.IsNullOrWhiteSpace(serverGateway) ? _activeProfile.Endpoint : $"{serverGateway}:{TryGetPort(_activeProfile.Endpoint)}",
            Address = deviceAddress,
            DnsServers = _activeProfile.DnsServers,
            Mtu = ParseNullableInt(_activeProfile.Mtu),
            AllowedIps = _activeProfile.AllowedIps,
            Routes = _activeProfile.AllowedIps,
            Warnings = BuildWarnings(_activeProfile).ToArray(),
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            LatestHandshakeAtUtc = handshake,
            ReceivedBytes = rxBytes,
            SentBytes = txBytes,
            AdapterPresent = connected,
            TunnelActive = connected,
            IsWindowsFirst = false,
            UsesSetConf = false
        };
    }

    private static IEnumerable<string> BuildWarnings(ImportedServerProfile profile)
    {
        if (profile.HasAwgMetadata)
        {
            yield return "AWG metadata will be applied through the Amnezia daemon path.";
        }

        if (profile.DnsServers.Count == 0)
        {
            yield return "The imported profile does not define DNS servers.";
        }
    }

    private static JsonObject BuildActivationPayload(ImportedServerProfile profile)
    {
        var config = profile.TunnelConfig;
        var endpoint = ParseEndpoint(config.Endpoint);
        var addresses = SplitAddresses(config.Address);
        var allowedRanges = new JsonArray();
        foreach (var allowedIp in config.AllowedIps)
        {
            var range = ParseCidr(allowedIp);
            allowedRanges.Add(new JsonObject
            {
                ["address"] = range.Address,
                ["range"] = range.PrefixLength,
                ["isIpv6"] = range.IsIpv6
            });
        }

        var json = new JsonObject
        {
            ["type"] = "activate",
            ["privateKey"] = TryGet(config.InterfaceValues, "PrivateKey"),
            ["deviceIpv4Address"] = addresses.Ipv4,
            ["deviceIpv6Address"] = string.IsNullOrWhiteSpace(addresses.Ipv6) ? DefaultIpv6Address : addresses.Ipv6,
            ["serverPublicKey"] = config.PublicKey ?? TryGet(config.PeerValues, "PublicKey"),
            ["serverPskKey"] = config.PresharedKey ?? TryGet(config.PeerValues, "PreSharedKey"),
            ["serverIpv4AddrIn"] = endpoint.Host,
            ["serverPort"] = endpoint.Port,
            ["serverIpv4Gateway"] = endpoint.Host,
            ["deviceMTU"] = config.Mtu ?? "1420",
            ["primaryDnsServer"] = config.DnsServers.FirstOrDefault() ?? string.Empty,
            ["secondaryDnsServer"] = config.DnsServers.Skip(1).FirstOrDefault() ?? string.Empty,
            ["allowedIPAddressRanges"] = allowedRanges,
            ["excludedAddresses"] = new JsonArray { endpoint.Host },
            ["vpnDisabledApps"] = new JsonArray(),
            ["allowedDnsServers"] = new JsonArray(),
            ["killSwitchOption"] = string.Empty
        };

        foreach (var pair in config.AwgValues)
        {
            json[pair.Key] = pair.Value;
        }

        return json;
    }

    private ConnectionState UpdateState(ConnectionState state)
    {
        _currentState = state;
        StateChanged?.Invoke(state);
        return state;
    }

    private static (string? Ipv4, string? Ipv6) SplitAddresses(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return (null, null);
        }

        string? ipv4 = null;
        string? ipv6 = null;
        foreach (var item in address.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (item.Contains(':'))
            {
                ipv6 = item;
            }
            else
            {
                ipv4 = item;
            }
        }

        return (ipv4, ipv6);
    }

    private static (string Host, int Port) ParseEndpoint(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException("Endpoint is required for the Amnezia daemon runtime.");
        }

        var trimmed = endpoint.Trim();
        if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.Contains("]:", StringComparison.Ordinal))
        {
            var separatorIndex = trimmed.LastIndexOf("]:", StringComparison.Ordinal);
            var host = trimmed[1..separatorIndex];
            var port = int.Parse(trimmed[(separatorIndex + 2)..], CultureInfo.InvariantCulture);
            return (host, port);
        }

        var lastColon = trimmed.LastIndexOf(':');
        if (lastColon < 0)
        {
            throw new InvalidOperationException("Endpoint must include a port.");
        }

        return (trimmed[..lastColon], int.Parse(trimmed[(lastColon + 1)..], CultureInfo.InvariantCulture));
    }

    private static (string Address, int PrefixLength, bool IsIpv6) ParseCidr(string cidr)
    {
        var parts = cidr.Split('/', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var prefix))
        {
            throw new InvalidOperationException($"Allowed IP '{cidr}' is not a valid CIDR range.");
        }

        return (parts[0], prefix, parts[0].Contains(':'));
    }

    private static string? TryGet(IReadOnlyDictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out var value) ? value : null;
    }

    private static int? ParseNullableInt(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static long TryGetInt64(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.TryGetInt64(out var value)
            ? value
            : 0L;
    }

    private static DateTimeOffset? TryParseDaemonDate(JsonElement element)
    {
        var dateText = TryGetString(element, "date");
        if (string.IsNullOrWhiteSpace(dateText))
        {
            return null;
        }

        return DateTimeOffset.TryParse(dateText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed)
            ? parsed
            : null;
    }

    private static int TryGetPort(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return 0;
        }

        return ParseEndpoint(endpoint).Port;
    }

    private static ImportedServerProfile? TryMatchProfile(JsonElement status, IReadOnlyList<ImportedServerProfile> profiles)
    {
        var gateway = TryGetString(status, "serverIpv4Gateway") ?? TryGetString(status, "serverIpv6Gateway");
        int? port = status.TryGetProperty("serverPort", out var portElement) && portElement.TryGetInt32(out var parsedPort)
            ? parsedPort
            : null;
        var deviceAddress = TryGetString(status, "deviceIpv4Address") ?? TryGetString(status, "deviceIpv6Address");

        var endpointMatches = profiles
            .Where(profile => MatchesEndpoint(profile.Endpoint, gateway, port))
            .ToArray();

        if (endpointMatches.Length == 1)
        {
            return endpointMatches[0];
        }

        if (!string.IsNullOrWhiteSpace(deviceAddress))
        {
            var addressMatches = profiles
                .Where(profile => string.Equals(
                    ExtractPrimaryAddress(profile.Address),
                    deviceAddress,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (addressMatches.Length == 1)
            {
                return addressMatches[0];
            }
        }

        return null;
    }

    private static bool MatchesEndpoint(string? profileEndpoint, string? gateway, int? port)
    {
        if (string.IsNullOrWhiteSpace(profileEndpoint) || string.IsNullOrWhiteSpace(gateway))
        {
            return false;
        }

        try
        {
            var parsed = ParseEndpoint(profileEndpoint);
            if (!string.Equals(parsed.Host, gateway, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return port is null || parsed.Port == port.Value;
        }
        catch
        {
            return false;
        }
    }

    private static string? ExtractPrimaryAddress(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return null;
        }

        var first = address.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(first))
        {
            return null;
        }

        var slashIndex = first.IndexOf('/');
        return slashIndex >= 0 ? first[..slashIndex] : first;
    }

    private static string? BuildDaemonEndpoint(JsonElement status)
    {
        var gateway = TryGetString(status, "serverIpv4Gateway") ?? TryGetString(status, "serverIpv6Gateway");
        if (string.IsNullOrWhiteSpace(gateway))
        {
            return null;
        }

        if (status.TryGetProperty("serverPort", out var portElement) && portElement.TryGetInt32(out var port))
        {
            return $"{gateway}:{port}";
        }

        return gateway;
    }
}
