using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
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
        ValidateRequired(request.UserExternalId, nameof(request.UserExternalId));
        ValidateRequired(request.DisplayName, nameof(request.DisplayName));
        ValidateRequired(request.EndpointHost, nameof(request.EndpointHost));

        var context = await LoadContextAsync(cancellationToken);
        var privateKey = (await ExecuteWgCommandAsync(["genkey"], cancellationToken)).Trim();
        var publicKey = (await ExecuteShellAsync($"printf '%s' '{EscapeShell(privateKey)}' | {EscapeShell(options.Value.WgExecutablePath)} pubkey", cancellationToken)).Trim();
        var presharedKey = (await ExecuteWgCommandAsync(["genpsk"], cancellationToken)).Trim();
        var allowedIps = AllocateNextPeerAddress(context.Config);
        var peer = PeerSection.Create(
            publicKey,
            presharedKey,
            allowedIps,
            request.UserExternalId,
            request.DisplayName,
            request.UserEmail,
            privateKey);

        context.Config.UpsertPeer(peer);
        context.ClientsTable.Upsert(ClientTableEntry.Create(peer.PublicKey, peer.DisplayName, peer.AllowedIps));

        await PersistAsync(context, cancellationToken);
        await ApplyPeerRuntimeAsync(context.InterfaceName, peer, cancellationToken);

        var clientConfig = BuildClientConfig(context.Config, context.ServerPublicKey, request.EndpointHost, peer);
        return new IssueAccessResponse(
            new AgentPeerMaterial(
                peer.PublicKey,
                peer.AllowedIps,
                peer.PresharedKey,
                privateKey,
                request.UserExternalId,
                request.DisplayName,
                request.UserEmail),
            BuildClientConfigFileName(request.DisplayName),
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
                request.Peer.PresharedKey,
                request.Peer.AllowedIps,
                request.Peer.UserExternalId,
                request.Peer.DisplayName,
                request.Peer.UserEmail,
                request.Peer.ClientPrivateKey);

            context.Config.UpsertPeer(peer);
            context.ClientsTable.Upsert(ClientTableEntry.Create(peer.PublicKey, peer.DisplayName, peer.AllowedIps));

            await PersistAsync(context, cancellationToken);
            await ApplyPeerRuntimeAsync(context.InterfaceName, peer, cancellationToken);

            string? clientConfig = null;
            string? fileName = null;
            if (!string.IsNullOrWhiteSpace(request.Peer.ClientPrivateKey) && !string.IsNullOrWhiteSpace(request.EndpointHost))
            {
                clientConfig = BuildClientConfig(context.Config, context.ServerPublicKey, request.EndpointHost, peer);
                fileName = BuildClientConfigFileName(peer.DisplayName);
            }

            return new SetAccessStateResponse(request.Peer.PublicKey, true, fileName, clientConfig);
        }

        context.Config.RemovePeer(request.Peer.PublicKey);
        context.ClientsTable.Remove(request.Peer.PublicKey);

        await PersistAsync(context, cancellationToken);
        await RemovePeerRuntimeAsync(context.InterfaceName, request.Peer.PublicKey, cancellationToken);

        return new SetAccessStateResponse(request.Peer.PublicKey, false, null, null);
    }

    private async Task<AgentContext> LoadContextAsync(CancellationToken cancellationToken)
    {
        var configPath = Path.Combine(options.Value.ConfigDirectory, "wg0.conf");
        var clientsTablePath = Path.Combine(options.Value.ConfigDirectory, "clientsTable");
        var serverPublicKeyPath = Path.Combine(options.Value.ConfigDirectory, "wireguard_server_public_key.key");

        var config = await LoadConfigAsync(configPath, cancellationToken);
        var clientsTable = await LoadClientsTableAsync(clientsTablePath, cancellationToken);
        var serverPublicKey = (await ReadFileTextAsync(serverPublicKeyPath, cancellationToken)).Trim();

        return new AgentContext(
            configPath,
            clientsTablePath,
            Path.GetFileNameWithoutExtension(configPath),
            serverPublicKey,
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

    private async Task PersistAsync(AgentContext context, CancellationToken cancellationToken)
    {
        await fileWriter.WriteAllTextAsync(context.ConfigPath, context.Config.Render(), cancellationToken);
        await fileWriter.WriteAllTextAsync(context.ClientsTablePath, context.ClientsTable.Render(), cancellationToken);
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

    private async Task ApplyPeerRuntimeAsync(string interfaceName, PeerSection peer, CancellationToken cancellationToken)
    {
        var commands = new List<string>();
        if (!string.IsNullOrWhiteSpace(peer.PresharedKey))
        {
            commands.Add($"tmp=$(mktemp)");
            commands.Add($"printf '%s' '{EscapeShell(peer.PresharedKey)}' > \"$tmp\"");
            commands.Add(
                $"{EscapeShell(options.Value.WgExecutablePath)} set {EscapeShell(interfaceName)} peer '{EscapeShell(peer.PublicKey)}' preshared-key \"$tmp\" allowed-ips '{EscapeShell(peer.AllowedIps)}'");
            commands.Add("rm -f \"$tmp\"");
        }
        else
        {
            commands.Add(
                $"{EscapeShell(options.Value.WgExecutablePath)} set {EscapeShell(interfaceName)} peer '{EscapeShell(peer.PublicKey)}' allowed-ips '{EscapeShell(peer.AllowedIps)}'");
        }

        await ExecuteShellAsync(string.Join("; ", commands), cancellationToken);
    }

    private Task RemovePeerRuntimeAsync(string interfaceName, string publicKey, CancellationToken cancellationToken)
    {
        return ExecuteShellAsync(
            $"{EscapeShell(options.Value.WgExecutablePath)} set {EscapeShell(interfaceName)} peer '{EscapeShell(publicKey)}' remove >/dev/null 2>&1 || true",
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
            || networkAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork
            || !int.TryParse(parts[1], out var prefixLength))
        {
            throw new InvalidOperationException($"Unsupported interface address format '{interfaceAddress}'.");
        }

        var usedAddresses = config.Peers
            .SelectMany(peer => peer.AllowedIps.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(value => value.Split('/', 2, StringSplitOptions.TrimEntries)[0])
            .Where(value => IPAddress.TryParse(value, out _))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var networkValue = ToUInt32(networkAddress);
        var hostBits = 32 - prefixLength;
        var subnetSize = hostBits <= 0 ? 1u : 1u << hostBits;
        var broadcast = networkValue + subnetSize - 1;

        for (var candidate = networkValue + 2; candidate < broadcast; candidate++)
        {
            var ip = FromUInt32(candidate).ToString();
            if (!usedAddresses.Contains(ip))
            {
                return $"{ip}/32";
            }
        }

        throw new InvalidOperationException("No free client addresses are available in the current subnet.");
    }

    private static string BuildClientConfig(
        WireGuardConfigDocument config,
        string serverPublicKey,
        string endpointHost,
        PeerSection peer)
    {
        var buffer = new StringBuilder();
        buffer.AppendLine("[Interface]");
        if (!string.IsNullOrWhiteSpace(peer.ClientPrivateKey))
        {
            buffer.AppendLine($"PrivateKey = {peer.ClientPrivateKey}");
        }

        buffer.AppendLine($"Address = {peer.AllowedIps}");

        foreach (var amneziaKey in new[] { "Jc", "Jmin", "Jmax", "S1", "S2", "H1", "H2", "H3", "H4" })
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
        buffer.AppendLine("AllowedIPs = 0.0.0.0/0, ::/0");
        buffer.AppendLine($"Endpoint = {endpointHost}:{listenPort}");
        buffer.AppendLine("PersistentKeepalive = 25");

        return buffer.ToString().TrimEnd();
    }

    private static string BuildClientConfigFileName(string displayName)
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

        return $"{safeName}.conf";
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

    private sealed record AgentContext(
        string ConfigPath,
        string ClientsTablePath,
        string InterfaceName,
        string ServerPublicKey,
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
