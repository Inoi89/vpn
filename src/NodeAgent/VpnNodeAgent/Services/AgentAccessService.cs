using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using VpnControlPlane.Contracts.Nodes;
using VpnNodeAgent.Abstractions;
using VpnNodeAgent.Configuration;

namespace VpnNodeAgent.Services;

public sealed class AgentAccessService(
    IConfigFileReader fileReader,
    IConfigFileWriter fileWriter,
    IOptions<AgentOptions> options,
    ProcessCommandExecutor commandExecutor) : IAgentAccessService
{
    private static readonly JsonSerializerOptions ClientsTableJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public async Task<IssueAccessResponse> IssueAsync(IssueAccessRequest request, CancellationToken cancellationToken)
    {
        ValidateRequired(request.DisplayName, nameof(request.DisplayName));
        ValidateRequired(request.EndpointHost, nameof(request.EndpointHost));

        var context = await LoadContextAsync(cancellationToken);
        var privateKey = (await ExecuteWgCommandAsync(["genkey"], cancellationToken)).Trim();
        var publicKey = (await ExecuteShellAsync($"printf '%s' '{EscapeShell(privateKey)}' | {EscapeShell(options.Value.WgExecutablePath)} pubkey", cancellationToken)).Trim();
        var presharedKey = !string.IsNullOrWhiteSpace(context.GlobalPresharedKey)
            ? context.GlobalPresharedKey
            : (await ExecuteWgCommandAsync(["genpsk"], cancellationToken)).Trim();
        var allowedIps = AllocateNextPeerAddress(context.Config);
        var peer = PeerSection.Create(
            publicKey,
            presharedKey,
            allowedIps,
            publicKey,
            request.DisplayName,
            request.UserEmail,
            privateKey);

        context.Config.UpsertPeer(peer);
        context.ClientsTable.Upsert(ClientTableEntry.Create(peer.PublicKey, peer.DisplayName, peer.AllowedIps));

        await PersistAsync(context, cancellationToken);
        await SyncRuntimeConfigAsync(context.InterfaceName, context.ConfigPath, cancellationToken);

        var (clientConfigFileName, clientConfig) = BuildClientExport(
            context.Config,
            context.ServerPublicKey,
            request.EndpointHost,
            peer,
            options.Value,
            request.Format);
        return new IssueAccessResponse(
            new AgentPeerMaterial(
                peer.PublicKey,
                peer.AllowedIps,
                peer.PresharedKey,
                privateKey,
                publicKey,
                request.DisplayName,
                request.UserEmail),
            clientConfigFileName,
            clientConfig);
    }

    public async Task<SetAccessStateResponse> SetStateAsync(SetAccessStateRequest request, CancellationToken cancellationToken)
    {
        ValidateRequired(request.Peer.PublicKey, nameof(request.Peer.PublicKey));

        var context = await LoadContextAsync(cancellationToken);

        if (request.IsEnabled)
        {
            ValidateRequired(request.Peer.AllowedIps, nameof(request.Peer.AllowedIps));
            ValidateRequired(request.Peer.DisplayName, nameof(request.Peer.DisplayName));
            ValidateRequired(request.Peer.UserExternalId, nameof(request.Peer.UserExternalId));

            var peer = PeerSection.Create(
                request.Peer.PublicKey,
                string.IsNullOrWhiteSpace(request.Peer.PresharedKey) ? context.GlobalPresharedKey : request.Peer.PresharedKey,
                request.Peer.AllowedIps,
                request.Peer.UserExternalId,
                request.Peer.DisplayName,
                request.Peer.UserEmail,
                request.Peer.ClientPrivateKey);

            context.Config.UpsertPeer(peer);
            context.ClientsTable.Upsert(ClientTableEntry.Create(peer.PublicKey, peer.DisplayName, peer.AllowedIps));

            await PersistAsync(context, cancellationToken);
            await SyncRuntimeConfigAsync(context.InterfaceName, context.ConfigPath, cancellationToken);

            string? clientConfig = null;
            string? fileName = null;
            if (!string.IsNullOrWhiteSpace(request.Peer.ClientPrivateKey) && !string.IsNullOrWhiteSpace(request.EndpointHost))
            {
                (fileName, clientConfig) = BuildClientExport(
                    context.Config,
                    context.ServerPublicKey,
                    request.EndpointHost,
                    peer,
                    options.Value,
                    AccessConfigFormats.AmneziaAwgNative);
            }

            return new SetAccessStateResponse(request.Peer.PublicKey, true, fileName, clientConfig);
        }

        await RemovePeerAsync(context, request.Peer.PublicKey, cancellationToken);

        return new SetAccessStateResponse(request.Peer.PublicKey, false, null, null);
    }

    public async Task<DeleteAccessResponse> DeleteAsync(DeleteAccessRequest request, CancellationToken cancellationToken)
    {
        ValidateRequired(request.PublicKey, nameof(request.PublicKey));

        var context = await LoadContextAsync(cancellationToken);
        await RemovePeerAsync(context, request.PublicKey, cancellationToken);

        return new DeleteAccessResponse(request.PublicKey, true);
    }

    public async Task<GetAccessConfigResponse> GetConfigAsync(GetAccessConfigRequest request, CancellationToken cancellationToken)
    {
        ValidateRequired(request.Peer.PublicKey, nameof(request.Peer.PublicKey));
        ValidateRequired(request.Peer.AllowedIps, nameof(request.Peer.AllowedIps));
        ValidateRequired(request.Peer.DisplayName, nameof(request.Peer.DisplayName));
        ValidateRequired(request.EndpointHost, nameof(request.EndpointHost));

        var context = await LoadContextAsync(cancellationToken);
        var clientPrivateKey = request.Peer.ClientPrivateKey;
        if (string.IsNullOrWhiteSpace(clientPrivateKey))
        {
            throw new InvalidOperationException("Client private key is unavailable for this access.");
        }

        var peer = PeerSection.Create(
            request.Peer.PublicKey,
            string.IsNullOrWhiteSpace(request.Peer.PresharedKey) ? context.GlobalPresharedKey : request.Peer.PresharedKey,
            request.Peer.AllowedIps,
            request.Peer.UserExternalId,
            request.Peer.DisplayName,
            request.Peer.UserEmail,
            clientPrivateKey);

        var (fileName, clientConfig) = BuildClientExport(
            context.Config,
            context.ServerPublicKey,
            request.EndpointHost,
            peer,
            options.Value,
            request.Format);
        return new GetAccessConfigResponse(peer.PublicKey, fileName, clientConfig);
    }

    private async Task<AgentContext> LoadContextAsync(CancellationToken cancellationToken)
    {
        var configPath = Path.Combine(options.Value.ConfigDirectory, "wg0.conf");
        var clientsTablePath = Path.Combine(options.Value.ConfigDirectory, "clientsTable");
        var serverPublicKeyPath = Path.Combine(options.Value.ConfigDirectory, "wireguard_server_public_key.key");
        var presharedKeyPath = Path.Combine(options.Value.ConfigDirectory, "wireguard_psk.key");

        var config = await LoadConfigAsync(configPath, cancellationToken);
        var clientsTable = await LoadClientsTableAsync(clientsTablePath, cancellationToken);
        var serverPublicKey = (await ReadFileTextAsync(serverPublicKeyPath, cancellationToken)).Trim();
        var globalPresharedKey = await TryReadOptionalTextAsync(presharedKeyPath, cancellationToken);

        return new AgentContext(
            configPath,
            clientsTablePath,
            Path.GetFileNameWithoutExtension(configPath),
            serverPublicKey,
            globalPresharedKey,
            config,
            clientsTable);
    }

    private async Task<WireGuardConfigDocument> LoadConfigAsync(string filePath, CancellationToken cancellationToken)
    {
        var lines = await fileReader.ReadAllLinesAsync(filePath, cancellationToken);
        return WireGuardConfigDocument.Parse(lines);
    }

    private async Task<ClientsTableDocument> LoadClientsTableAsync(string filePath, CancellationToken cancellationToken)
    {
        var raw = await ReadFileTextAsync(filePath, cancellationToken);
        return ClientsTableDocument.Parse(raw);
    }

    private async Task<string> ReadFileTextAsync(string filePath, CancellationToken cancellationToken)
    {
        var lines = await fileReader.ReadAllLinesAsync(filePath, cancellationToken);
        return string.Join('\n', lines);
    }

    private async Task<string?> TryReadOptionalTextAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            var text = await ReadFileTextAsync(filePath, cancellationToken);
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }
        catch
        {
            return null;
        }
    }

    private async Task PersistAsync(AgentContext context, CancellationToken cancellationToken)
    {
        await fileWriter.WriteAllTextAsync(context.ConfigPath, context.Config.Render(), cancellationToken);
        await fileWriter.WriteAllTextAsync(context.ClientsTablePath, context.ClientsTable.Render(), cancellationToken);
    }

    private async Task RemovePeerAsync(AgentContext context, string publicKey, CancellationToken cancellationToken)
    {
        context.Config.RemovePeer(publicKey);
        context.ClientsTable.Remove(publicKey);

        await PersistAsync(context, cancellationToken);
        await SyncRuntimeConfigAsync(context.InterfaceName, context.ConfigPath, cancellationToken);
    }

    private async Task<string> ExecuteWgCommandAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (string.Equals(options.Value.OperationMode, "Docker", StringComparison.OrdinalIgnoreCase))
        {
            return await commandExecutor.ExecuteAsync(
                options.Value.DockerExecutablePath,
                ["exec", GetRequiredContainerName(), options.Value.WgExecutablePath, .. arguments],
                cancellationToken);
        }

        return await commandExecutor.ExecuteAsync(options.Value.WgExecutablePath, arguments, cancellationToken);
    }

    private async Task<string> ExecuteShellAsync(string script, CancellationToken cancellationToken)
    {
        if (string.Equals(options.Value.OperationMode, "Docker", StringComparison.OrdinalIgnoreCase))
        {
            return await commandExecutor.ExecuteAsync(
                options.Value.DockerExecutablePath,
                ["exec", GetRequiredContainerName(), "sh", "-lc", script],
                cancellationToken);
        }

        return await commandExecutor.ExecuteAsync("sh", ["-lc", script], cancellationToken);
    }

    private Task SyncRuntimeConfigAsync(string interfaceName, string configPath, CancellationToken cancellationToken)
    {
        return ExecuteShellAsync(
            $"if {EscapeShell(options.Value.WgExecutablePath)} show '{EscapeShell(interfaceName)}' >/dev/null 2>&1; then " +
            "tmp=$(mktemp); " +
            $"{EscapeShell(options.Value.WgQuickExecutablePath)} strip '{EscapeShell(configPath)}' > \"$tmp\"; " +
            $"{EscapeShell(options.Value.WgExecutablePath)} syncconf '{EscapeShell(interfaceName)}' \"$tmp\"; " +
            "rm -f \"$tmp\"; " +
            "else " +
            $"{EscapeShell(options.Value.WgQuickExecutablePath)} up '{EscapeShell(configPath)}'; " +
            "fi",
            cancellationToken);
    }

    private static string AllocateNextPeerAddress(WireGuardConfigDocument config)
    {
        var interfaceAddress = config.GetInterfaceValue("Address")
            ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(value => value.Contains('.', StringComparison.Ordinal))
            ?? throw new InvalidOperationException("Interface Address is required to allocate a peer address.");

        var parts = interfaceAddress.Split('/', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2
            || !IPAddress.TryParse(parts[0], out var networkAddress)
            || networkAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            throw new InvalidOperationException($"Unsupported interface address format '{interfaceAddress}'.");
        }

        var seedAddress = config.GetLastPeerIpv4Address() ?? FromUInt32(ToUInt32(networkAddress) + 1);
        return $"{GetNextSequentialPeerAddress(seedAddress)}/32";
    }

    private static (string FileName, string Payload) BuildClientExport(
        WireGuardConfigDocument config,
        string serverPublicKey,
        string endpointHost,
        PeerSection peer,
        AgentOptions agentOptions,
        string? format)
    {
        var normalizedFormat = AccessConfigFormats.Normalize(format);
        var clientConfig = BuildClientConfig(config, serverPublicKey, endpointHost, peer, agentOptions, useDnsPlaceholders: false);

        if (string.Equals(normalizedFormat, AccessConfigFormats.AmneziaVpn, StringComparison.OrdinalIgnoreCase))
        {
            return (BuildClientConfigFileName(peer.DisplayName, ".vpn"), BuildAmneziaVpnConfig(config, serverPublicKey, endpointHost, peer, agentOptions));
        }

        return (BuildClientConfigFileName(peer.DisplayName, ".conf"), clientConfig);
    }

    private static string BuildClientConfig(
        WireGuardConfigDocument config,
        string serverPublicKey,
        string endpointHost,
        PeerSection peer,
        AgentOptions agentOptions,
        bool useDnsPlaceholders)
    {
        var dnsServers = GetClientDnsServers(agentOptions);
        var allowedIps = GetClientTunnelRoutes(agentOptions);
        var buffer = new StringBuilder();
        buffer.AppendLine("[Interface]");
        buffer.AppendLine($"Address = {peer.AllowedIps}");
        if (dnsServers.Count > 0)
        {
            buffer.AppendLine($"DNS = {BuildDnsValue(dnsServers, useDnsPlaceholders)}");
        }

        if (!string.IsNullOrWhiteSpace(peer.ClientPrivateKey))
        {
            buffer.AppendLine($"PrivateKey = {peer.ClientPrivateKey}");
        }

        if (agentOptions.DefaultClientMtu > 0)
        {
            buffer.AppendLine($"MTU = {agentOptions.DefaultClientMtu.ToString(CultureInfo.InvariantCulture)}");
        }

        foreach (var amneziaKey in new[] { "Jc", "Jmin", "Jmax", "S1", "S2", "S3", "S4", "H1", "H2", "H3", "H4", "I1", "I2", "I3", "I4", "I5" })
        {
            var value = config.GetInterfaceValue(amneziaKey);
            if (!string.IsNullOrWhiteSpace(value))
            {
                buffer.AppendLine($"{amneziaKey} = {value}");
            }
        }

        buffer.AppendLine();
        buffer.AppendLine("[Peer]");
        buffer.AppendLine($"PublicKey = {serverPublicKey}");
        if (!string.IsNullOrWhiteSpace(peer.PresharedKey))
        {
            buffer.AppendLine($"PresharedKey = {peer.PresharedKey}");
        }

        var listenPort = config.GetInterfaceValue("ListenPort") ?? "51820";
        buffer.AppendLine($"AllowedIPs = {string.Join(", ", allowedIps)}");
        buffer.AppendLine($"Endpoint = {endpointHost}:{listenPort}");
        buffer.AppendLine("PersistentKeepalive = 25");

        return buffer.ToString().TrimEnd();
    }

    private static string BuildAmneziaVpnConfig(
        WireGuardConfigDocument config,
        string serverPublicKey,
        string endpointHost,
        PeerSection peer,
        AgentOptions agentOptions)
    {
        var dnsServers = GetClientDnsServers(agentOptions);
        var allowedIps = GetClientTunnelRoutes(agentOptions);
        var listenPort = config.GetInterfaceValue("ListenPort") ?? "51820";
        var clientConfig = BuildClientConfig(config, serverPublicKey, endpointHost, peer, agentOptions, useDnsPlaceholders: dnsServers.Count > 0);
        var lastConfig = new JsonObject
        {
            ["config"] = clientConfig,
            ["hostName"] = endpointHost,
            ["port"] = int.TryParse(listenPort, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port) ? port : 51820,
            ["clientId"] = peer.PublicKey,
            ["client_priv_key"] = peer.ClientPrivateKey,
            ["client_ip"] = StripCidrSuffix(peer.AllowedIps),
            ["client_pub_key"] = peer.PublicKey,
            ["psk_key"] = peer.PresharedKey,
            ["server_pub_key"] = serverPublicKey,
            ["mtu"] = agentOptions.DefaultClientMtu.ToString(CultureInfo.InvariantCulture),
            ["persistent_keep_alive"] = "25",
            ["allowed_ips"] = new JsonArray(allowedIps.Select(route => JsonValue.Create(route)).ToArray<JsonNode?>())
        };

        foreach (var amneziaKey in new[] { "Jc", "Jmin", "Jmax", "S1", "S2", "S3", "S4", "H1", "H2", "H3", "H4", "I1", "I2", "I3", "I4", "I5" })
        {
            var value = config.GetInterfaceValue(amneziaKey);
            if (!string.IsNullOrWhiteSpace(value))
            {
                lastConfig[amneziaKey] = value;
            }
        }

        var awgConfig = new JsonObject
        {
            ["last_config"] = lastConfig.ToJsonString(),
            ["port"] = listenPort,
            ["transport_proto"] = "udp"
        };

        foreach (var amneziaKey in new[] { "Jc", "Jmin", "Jmax", "S1", "S2", "S3", "S4", "H1", "H2", "H3", "H4", "I1", "I2", "I3", "I4", "I5" })
        {
            var value = config.GetInterfaceValue(amneziaKey);
            if (!string.IsNullOrWhiteSpace(value))
            {
                awgConfig[amneziaKey] = value;
            }
        }

        var hasAwgV2Fields = awgConfig["S3"] is not null && awgConfig["S4"] is not null;
        var hasAwgV15Fields = awgConfig["I1"] is not null
                              || awgConfig["I2"] is not null
                              || awgConfig["I3"] is not null
                              || awgConfig["I4"] is not null
                              || awgConfig["I5"] is not null;
        if (hasAwgV2Fields)
        {
            awgConfig["protocol_version"] = "2";
        }
        else if (hasAwgV15Fields)
        {
            awgConfig["protocol_version"] = "1.5";
        }

        var payload = new JsonObject
        {
            ["containers"] = new JsonArray(
                new JsonObject
                {
                    ["container"] = "amnezia-awg",
                    ["awg"] = awgConfig
                }),
            ["defaultContainer"] = "amnezia-awg",
            ["description"] = endpointHost,
            ["hostName"] = endpointHost
        };

        if (dnsServers.Count > 0)
        {
            payload["dns1"] = dnsServers[0];
        }

        if (dnsServers.Count > 1)
        {
            payload["dns2"] = dnsServers[1];
        }

        var json = payload.ToJsonString();
        var bytes = Encoding.UTF8.GetBytes(json);
        var compressed = QtCompress(bytes);
        return $"vpn://{ToBase64Url(compressed)}";
    }

    private static byte[] QtCompress(byte[] payload)
    {
        using var output = new MemoryStream();

        var length = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(payload.Length));
        output.Write(length, 0, length.Length);

        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(payload, 0, payload.Length);
        }

        return output.ToArray();
    }

    private static string ToBase64Url(byte[] payload)
    {
        return Convert.ToBase64String(payload)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string BuildClientConfigFileName(string displayName, string extension)
    {
        var safeName = new string(
            displayName
                .Trim()
                .Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-')
                .ToArray())
            .Trim('-');

        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "vpn-access";
        }

        return $"{safeName}{extension}";
    }

    private static string StripCidrSuffix(string value)
    {
        var separatorIndex = value.IndexOf('/');
        return separatorIndex >= 0 ? value[..separatorIndex] : value;
    }

    private static IReadOnlyList<string> GetClientDnsServers(AgentOptions agentOptions)
    {
        return agentOptions.ClientDnsServers
            .Where(server => !string.IsNullOrWhiteSpace(server))
            .Select(server => server.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToArray();
    }

    private static IReadOnlyList<string> GetClientTunnelRoutes(AgentOptions agentOptions)
    {
        var routes = new List<string> { "0.0.0.0/0" };
        if (agentOptions.IncludeIpv6DefaultRoute)
        {
            routes.Add("::/0");
        }

        return routes;
    }

    private static string BuildDnsValue(IReadOnlyList<string> dnsServers, bool useDnsPlaceholders)
    {
        if (!useDnsPlaceholders)
        {
            return string.Join(", ", dnsServers);
        }

        return dnsServers.Count switch
        {
            > 1 => "$PRIMARY_DNS, $SECONDARY_DNS",
            1 => "$PRIMARY_DNS",
            _ => string.Empty
        };
    }

    private static void ValidateRequired(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", paramName);
        }
    }

    private static string EscapeShell(string value)
    {
        return value.Replace("'", "'\"'\"'", StringComparison.Ordinal);
    }

    private string GetRequiredContainerName()
    {
        if (string.IsNullOrWhiteSpace(options.Value.DockerContainerName))
        {
            throw new InvalidOperationException("Agent:DockerContainerName is required when Agent:OperationMode is Docker.");
        }

        return options.Value.DockerContainerName;
    }

    private static uint ToUInt32(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }

        return BitConverter.ToUInt32(bytes, 0);
    }

    private static IPAddress FromUInt32(uint value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }

        return new IPAddress(bytes);
    }

    private static IPAddress GetNextSequentialPeerAddress(IPAddress previousAddress)
    {
        var previousValue = ToUInt32(previousAddress);
        var lastOctet = previousAddress.GetAddressBytes()[3];
        var increment = lastOctet switch
        {
            254 => 3u,
            255 => 2u,
            _ => 1u
        };

        return FromUInt32(previousValue + increment);
    }

    private sealed record AgentContext(
        string ConfigPath,
        string ClientsTablePath,
        string InterfaceName,
        string ServerPublicKey,
        string? GlobalPresharedKey,
        WireGuardConfigDocument Config,
        ClientsTableDocument ClientsTable);

    private sealed class WireGuardConfigDocument(
        List<KeyValueEntry> interfaceEntries,
        List<PeerSection> peers)
    {
        public List<KeyValueEntry> InterfaceEntries { get; } = interfaceEntries;

        public List<PeerSection> Peers { get; } = peers;

        public string? GetInterfaceValue(string key)
        {
            return InterfaceEntries.LastOrDefault(entry => string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase))?.Value;
        }

        public IPAddress? GetLastPeerIpv4Address()
        {
            foreach (var peer in Peers.AsEnumerable().Reverse())
            {
                var ipv4Address = peer.AllowedIps
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(value => value.Split('/', 2, StringSplitOptions.TrimEntries)[0])
                    .FirstOrDefault(value => IPAddress.TryParse(value, out var address)
                                             && address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

                if (!string.IsNullOrWhiteSpace(ipv4Address) && IPAddress.TryParse(ipv4Address, out var address))
                {
                    return address;
                }
            }

            return null;
        }

        public void UpsertPeer(PeerSection peer)
        {
            var existing = Peers.FindIndex(item => string.Equals(item.PublicKey, peer.PublicKey, StringComparison.OrdinalIgnoreCase));
            if (existing >= 0)
            {
                Peers[existing] = peer;
                return;
            }

            Peers.Add(peer);
        }

        public void RemovePeer(string publicKey)
        {
            Peers.RemoveAll(peer => string.Equals(peer.PublicKey, publicKey, StringComparison.OrdinalIgnoreCase));
        }

        public string Render()
        {
            var buffer = new StringBuilder();
            buffer.AppendLine("[Interface]");
            foreach (var entry in InterfaceEntries)
            {
                buffer.AppendLine($"{entry.Key} = {entry.Value}");
            }

            foreach (var peer in Peers)
            {
                buffer.AppendLine();
                buffer.AppendLine("[Peer]");
                foreach (var metadata in peer.Metadata)
                {
                    buffer.AppendLine($"# {metadata.Key} = {metadata.Value}");
                }

                foreach (var entry in peer.Properties)
                {
                    buffer.AppendLine($"{entry.Key} = {entry.Value}");
                }
            }

            return buffer.ToString().TrimEnd() + Environment.NewLine;
        }

        public static WireGuardConfigDocument Parse(IReadOnlyList<string> lines)
        {
            var interfaceEntries = new List<KeyValueEntry>();
            var peers = new List<PeerSection>();
            string? section = null;
            List<KeyValueEntry>? currentPeerEntries = null;
            Dictionary<string, string>? currentPeerMetadata = null;

            void FlushPeer()
            {
                if (!string.Equals(section, "Peer", StringComparison.OrdinalIgnoreCase) || currentPeerEntries is null)
                {
                    return;
                }

                var publicKey = currentPeerEntries
                    .LastOrDefault(entry => string.Equals(entry.Key, "PublicKey", StringComparison.OrdinalIgnoreCase))
                    ?.Value;

                if (string.IsNullOrWhiteSpace(publicKey))
                {
                    return;
                }

                peers.Add(new PeerSection(
                    currentPeerEntries,
                    currentPeerMetadata ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)));
            }

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (line.StartsWith('[') && line.EndsWith(']'))
                {
                    FlushPeer();
                    section = line.Trim('[', ']');
                    currentPeerEntries = string.Equals(section, "Peer", StringComparison.OrdinalIgnoreCase) ? [] : null;
                    currentPeerMetadata = string.Equals(section, "Peer", StringComparison.OrdinalIgnoreCase)
                        ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        : null;
                    continue;
                }

                if ((line.StartsWith('#') || line.StartsWith(';'))
                    && string.Equals(section, "Peer", StringComparison.OrdinalIgnoreCase)
                    && currentPeerMetadata is not null)
                {
                    var comment = line[1..].Trim();
                    var separatorIndex = comment.IndexOf('=');
                    if (separatorIndex > 0)
                    {
                        currentPeerMetadata[comment[..separatorIndex].Trim()] = comment[(separatorIndex + 1)..].Trim();
                    }

                    continue;
                }

                var parts = line.Split('=', 2, StringSplitOptions.TrimEntries);
                if (parts.Length != 2)
                {
                    continue;
                }

                var entry = new KeyValueEntry(parts[0], parts[1]);
                if (string.Equals(section, "Interface", StringComparison.OrdinalIgnoreCase))
                {
                    interfaceEntries.Add(entry);
                }
                else if (string.Equals(section, "Peer", StringComparison.OrdinalIgnoreCase) && currentPeerEntries is not null)
                {
                    currentPeerEntries.Add(entry);
                }
            }

            FlushPeer();
            return new WireGuardConfigDocument(interfaceEntries, peers);
        }
    }

    private sealed class PeerSection(
        List<KeyValueEntry> properties,
        Dictionary<string, string> metadata)
    {
        public List<KeyValueEntry> Properties { get; } = properties;

        public Dictionary<string, string> Metadata { get; } = metadata;

        public string PublicKey => GetRequiredValue("PublicKey");

        public string AllowedIps => GetRequiredValue("AllowedIPs");

        public string DisplayName => Metadata.GetValueOrDefault("vpn-display-name", PublicKey);

        public string? PresharedKey => Properties.LastOrDefault(entry => string.Equals(entry.Key, "PresharedKey", StringComparison.OrdinalIgnoreCase))?.Value;

        public string? ClientPrivateKey => Metadata.GetValueOrDefault("vpn-client-private-key");

        private string GetRequiredValue(string key)
        {
            return Properties.Last(entry => string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase)).Value;
        }

        public static PeerSection Create(
            string publicKey,
            string? presharedKey,
            string allowedIps,
            string userExternalId,
            string displayName,
            string? userEmail,
            string? clientPrivateKey)
        {
            var properties = new List<KeyValueEntry>
            {
                new("PublicKey", publicKey),
                new("AllowedIPs", allowedIps)
            };

            if (!string.IsNullOrWhiteSpace(presharedKey))
            {
                properties.Insert(1, new KeyValueEntry("PresharedKey", presharedKey));
            }

            var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["vpn-user-id"] = userExternalId,
                ["vpn-display-name"] = displayName
            };

            if (!string.IsNullOrWhiteSpace(userEmail))
            {
                metadata["vpn-email"] = userEmail;
            }

            if (!string.IsNullOrWhiteSpace(clientPrivateKey))
            {
                metadata["vpn-client-private-key"] = clientPrivateKey;
            }

            metadata["vpn-issued-at"] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            return new PeerSection(properties, metadata);
        }
    }

    private sealed record KeyValueEntry(string Key, string Value);

    private sealed class ClientsTableDocument(List<ClientTableEntry> entries)
    {
        public List<ClientTableEntry> Entries { get; } = entries;

        public void Upsert(ClientTableEntry entry)
        {
            var existing = Entries.FindIndex(item => string.Equals(item.ClientId, entry.ClientId, StringComparison.OrdinalIgnoreCase));
            if (existing >= 0)
            {
                Entries[existing] = entry;
                return;
            }

            Entries.Add(entry);
        }

        public void Remove(string publicKey)
        {
            Entries.RemoveAll(entry => string.Equals(entry.ClientId, publicKey, StringComparison.OrdinalIgnoreCase));
        }

        public string Render()
        {
            return JsonSerializer.Serialize(Entries, ClientsTableJsonOptions);
        }

        public static ClientsTableDocument Parse(string rawJson)
        {
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                return new ClientsTableDocument([]);
            }

            var entries = JsonSerializer.Deserialize<List<ClientTableEntry>>(rawJson, ClientsTableJsonOptions) ?? [];
            return new ClientsTableDocument(entries);
        }
    }

    private sealed class ClientTableEntry
    {
        public string ClientId { get; set; } = string.Empty;

        public ClientUserData UserData { get; set; } = new();

        public static ClientTableEntry Create(string publicKey, string displayName, string allowedIps)
        {
            return new ClientTableEntry
            {
                ClientId = publicKey,
                UserData = new ClientUserData
                {
                    AllowedIps = allowedIps,
                    ClientName = displayName,
                    CreationDate = DateTimeOffset.UtcNow.ToString("ddd MMM d HH:mm:ss yyyy", CultureInfo.InvariantCulture),
                    DataReceived = "0 B",
                    DataSent = "0 B",
                    LatestHandshake = "never"
                }
            };
        }
    }

    private sealed class ClientUserData
    {
        public string AllowedIps { get; set; } = string.Empty;

        public string ClientName { get; set; } = string.Empty;

        public string CreationDate { get; set; } = string.Empty;

        public string DataReceived { get; set; } = string.Empty;

        public string DataSent { get; set; } = string.Empty;

        public string LatestHandshake { get; set; } = string.Empty;
    }
}
