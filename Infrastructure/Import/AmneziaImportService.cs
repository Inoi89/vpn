using System.IO.Compression;
using System.Text;
using System.Text.Json;
using VpnClient.Core.Interfaces;
using VpnClient.Core.Models;

namespace VpnClient.Infrastructure.Import;

public sealed class AmneziaImportService : IImportService
{
    public async Task<ImportedTunnelConfig> ImportAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path is required.", nameof(path));
        }

        var fullPath = Path.GetFullPath(path.Trim());
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Configuration file was not found.", fullPath);
        }

        var rawSource = await File.ReadAllTextAsync(fullPath, cancellationToken);
        var fileName = Path.GetFileName(fullPath);
        return await ImportFromContentAsync(fileName, rawSource, fullPath, cancellationToken);
    }

    public Task<ImportedTunnelConfig> ImportFromContentAsync(
        string fileName,
        string rawSource,
        string? sourcePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("File name is required.", nameof(fileName));
        }

        if (string.IsNullOrWhiteSpace(rawSource))
        {
            throw new InvalidOperationException("Configuration content is empty.");
        }

        var normalizedFileName = fileName.Trim();
        var normalizedSourcePath = string.IsNullOrWhiteSpace(sourcePath)
            ? $"memory://{normalizedFileName}"
            : sourcePath.Trim();
        var extension = Path.GetExtension(normalizedFileName);
        var trimmed = rawSource.Trim();

        ImportedTunnelConfig imported = string.Equals(extension, ".vpn", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("vpn://", StringComparison.OrdinalIgnoreCase)
            ? ImportVpn(normalizedSourcePath, normalizedFileName, rawSource)
            : ImportNative(normalizedSourcePath, normalizedFileName, rawSource);

        return Task.FromResult(imported);
    }

    private static ImportedTunnelConfig ImportNative(string sourcePath, string fileName, string rawSource)
    {
        var normalized = NormalizeLineEndings(rawSource).Trim();
        if (!normalized.Contains("[Interface]", StringComparison.OrdinalIgnoreCase)
            || !normalized.Contains("[Peer]", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The imported file is not a valid WireGuard/AmneziaWG config.");
        }

        var tunnelConfig = ParseTunnelConfig(normalized, TunnelConfigFormat.AmneziaAwgNative);
        var displayName = Path.GetFileNameWithoutExtension(fileName);

        return new ImportedTunnelConfig(
            string.IsNullOrWhiteSpace(displayName) ? "Imported server" : displayName,
            fileName,
            sourcePath,
            tunnelConfig.Format,
            DateTimeOffset.UtcNow,
            rawSource,
            null,
            tunnelConfig);
    }

    private static ImportedTunnelConfig ImportVpn(string sourcePath, string fileName, string rawSource)
    {
        var payloadText = rawSource.Trim();
        if (payloadText.StartsWith("vpn://", StringComparison.OrdinalIgnoreCase))
        {
            payloadText = payloadText["vpn://".Length..];
        }

        var payloadBytes = DecodeBase64Url(payloadText);
        var packageJson = DecodeVpnPayload(payloadBytes);

        using var document = JsonDocument.Parse(packageJson);
        var root = document.RootElement;
        var rawConfig = ExtractTunnelConfig(root)
            ?? throw new InvalidOperationException("The imported .vpn file does not contain a usable tunnel config.");
        var materializedConfig = AmneziaVpnConfigMaterializer.Materialize(root, rawConfig);

        var tunnelConfig = ParseTunnelConfig(NormalizeLineEndings(materializedConfig).Trim(), TunnelConfigFormat.AmneziaVpn);
        var displayName = ExtractDisplayName(root)
            ?? Path.GetFileNameWithoutExtension(fileName);

        return new ImportedTunnelConfig(
            string.IsNullOrWhiteSpace(displayName) ? "Imported server" : displayName,
            fileName,
            sourcePath,
            tunnelConfig.Format,
            DateTimeOffset.UtcNow,
            rawSource,
            packageJson,
            tunnelConfig);
    }

    private static TunnelConfig ParseTunnelConfig(string rawConfig, TunnelConfigFormat preferredFormat)
    {
        var lines = ParseLines(rawConfig);
        var interfaceValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var peerValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var currentSection = string.Empty;

        foreach (var line in lines)
        {
            if (line.Kind == ConfigLineKind.SectionHeader)
            {
                currentSection = line.SectionName ?? string.Empty;
                continue;
            }

            if (line.Kind != ConfigLineKind.KeyValue || string.IsNullOrWhiteSpace(line.Key))
            {
                continue;
            }

            if (currentSection.Equals("Interface", StringComparison.OrdinalIgnoreCase))
            {
                interfaceValues[line.Key] = line.Value ?? string.Empty;
            }
            else if (currentSection.Equals("Peer", StringComparison.OrdinalIgnoreCase))
            {
                peerValues[line.Key] = line.Value ?? string.Empty;
            }
        }

        var interfaceReadOnly = new Dictionary<string, string>(interfaceValues, StringComparer.OrdinalIgnoreCase);
        var peerReadOnly = new Dictionary<string, string>(peerValues, StringComparer.OrdinalIgnoreCase);
        var awgValues = interfaceValues
            .Where(pair => IsAwgKey(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

        var address = TryGetValue(interfaceValues, "Address");
        var dnsServers = SplitList(TryGetValue(interfaceValues, "DNS"));
        var mtu = TryGetValue(interfaceValues, "MTU");
        var allowedIps = SplitList(TryGetValue(peerValues, "AllowedIPs"));
        var keepalive = TryParseInt(TryGetValue(peerValues, "PersistentKeepalive"));
        var endpoint = TryGetValue(peerValues, "Endpoint");
        var publicKey = TryGetValue(peerValues, "PublicKey");
        var presharedKey = TryGetValue(peerValues, "PresharedKey");
        var format = preferredFormat == TunnelConfigFormat.AmneziaVpn && awgValues.Count == 0
            ? TunnelConfigFormat.AmneziaVpn
            : preferredFormat;

        if (preferredFormat == TunnelConfigFormat.AmneziaAwgNative && awgValues.Count == 0)
        {
            format = TunnelConfigFormat.WireGuardConf;
        }

        return new TunnelConfig(
            format,
            rawConfig,
            lines,
            interfaceReadOnly,
            peerReadOnly,
            awgValues,
            address,
            dnsServers,
            mtu,
            allowedIps,
            keepalive,
            endpoint,
            publicKey,
            presharedKey);
    }

    private static IReadOnlyList<ConfigLine> ParseLines(string rawConfig)
    {
        var result = new List<ConfigLine>();
        var currentSection = string.Empty;
        var normalized = NormalizeLineEndings(rawConfig);
        var split = normalized.Split('\n');

        for (var index = 0; index < split.Length; index++)
        {
            var rawLine = split[index];
            var trimmed = rawLine.Trim();

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                result.Add(new ConfigLine(index, ConfigLineKind.Blank, rawLine, currentSection));
                continue;
            }

            if (trimmed.StartsWith("#", StringComparison.Ordinal) || trimmed.StartsWith(";", StringComparison.Ordinal))
            {
                result.Add(new ConfigLine(index, ConfigLineKind.Comment, rawLine, currentSection));
                continue;
            }

            if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal))
            {
                currentSection = trimmed[1..^1].Trim();
                result.Add(new ConfigLine(index, ConfigLineKind.SectionHeader, rawLine, currentSection));
                continue;
            }

            var equalsIndex = trimmed.IndexOf('=');
            if (equalsIndex > 0)
            {
                var key = trimmed[..equalsIndex].Trim();
                var value = trimmed[(equalsIndex + 1)..].Trim();
                result.Add(new ConfigLine(index, ConfigLineKind.KeyValue, rawLine, currentSection, key, value));
                continue;
            }

            result.Add(new ConfigLine(index, ConfigLineKind.Unknown, rawLine, currentSection));
        }

        return result;
    }

    private static string? ExtractTunnelConfig(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            var value = element.GetString();
            if (LooksLikeTunnelConfig(value))
            {
                return value;
            }

            return null;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var candidateName in new[] { "last_config", "config" })
            {
                if (!element.TryGetProperty(candidateName, out var candidate))
                {
                    continue;
                }

                if (candidate.ValueKind == JsonValueKind.String)
                {
                    var candidateValue = candidate.GetString();
                    if (string.IsNullOrWhiteSpace(candidateValue))
                    {
                        continue;
                    }

                    var trimmedCandidate = candidateValue.TrimStart();
                    var looksLikeJson = trimmedCandidate.StartsWith("{", StringComparison.Ordinal)
                        || trimmedCandidate.StartsWith("[", StringComparison.Ordinal);

                    if (looksLikeJson && TryParseJsonDocumentLoose(candidateValue, out var nestedDocument))
                    {
                        using (nestedDocument)
                        {
                            var nestedConfig = ExtractTunnelConfig(nestedDocument.RootElement);
                            if (!string.IsNullOrWhiteSpace(nestedConfig))
                            {
                                return nestedConfig;
                            }
                        }
                    }

                    if (LooksLikeTunnelConfig(candidateValue))
                    {
                        return candidateValue;
                    }
                }
                else
                {
                    var nestedConfig = ExtractTunnelConfig(candidate);
                    if (!string.IsNullOrWhiteSpace(nestedConfig))
                    {
                        return nestedConfig;
                    }
                }
            }

            foreach (var property in element.EnumerateObject())
            {
                var nestedConfig = ExtractTunnelConfig(property.Value);
                if (!string.IsNullOrWhiteSpace(nestedConfig))
                {
                    return nestedConfig;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var nestedConfig = ExtractTunnelConfig(item);
                if (!string.IsNullOrWhiteSpace(nestedConfig))
                {
                    return nestedConfig;
                }
            }
        }

        return null;
    }

    private static bool TryParseJsonDocumentLoose(string json, out JsonDocument document)
    {
        try
        {
            document = JsonDocument.Parse(json);
            return true;
        }
        catch
        {
            var escaped = json
                .Replace("\r\n", "\\r\\n")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r");

            try
            {
                document = JsonDocument.Parse(escaped);
                return true;
            }
            catch
            {
                document = null!;
                return false;
            }
        }
    }

    private static string? ExtractDisplayName(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var candidateName in new[] { "display_name", "displayName", "server_name", "serverName", "name" })
            {
                if (element.TryGetProperty(candidateName, out var candidate)
                    && candidate.ValueKind == JsonValueKind.String)
                {
                    var value = candidate.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
            }

            foreach (var property in element.EnumerateObject())
            {
                var displayName = ExtractDisplayName(property.Value);
                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    return displayName;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var displayName = ExtractDisplayName(item);
                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    return displayName;
                }
            }
        }

        return null;
    }

    private static string DecodeVpnPayload(byte[] payloadBytes)
    {
        foreach (var offset in new[] { 4, 0 })
        {
            if (payloadBytes.Length <= offset)
            {
                continue;
            }

            try
            {
                using var source = new MemoryStream(payloadBytes, offset, payloadBytes.Length - offset, writable: false);
                using var zlib = new ZLibStream(source, CompressionMode.Decompress);
                using var output = new MemoryStream();
                zlib.CopyTo(output);
                return Encoding.UTF8.GetString(output.ToArray());
            }
            catch (InvalidDataException)
            {
                // Try the next offset.
            }
            catch (ArgumentException)
            {
                // Try the next offset.
            }
        }

        throw new InvalidOperationException("The imported .vpn file could not be decompressed.");
    }

    private static byte[] DecodeBase64Url(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');
        var padding = normalized.Length % 4;
        if (padding > 0)
        {
            normalized = normalized.PadRight(normalized.Length + (4 - padding), '=');
        }

        return Convert.FromBase64String(normalized);
    }

    private static bool LooksLikeTunnelConfig(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
               && value.Contains("[Interface]", StringComparison.OrdinalIgnoreCase)
               && value.Contains("[Peer]", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAwgKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        return key.Equals("Jc", StringComparison.OrdinalIgnoreCase)
               || key.Equals("Jmin", StringComparison.OrdinalIgnoreCase)
               || key.Equals("Jmax", StringComparison.OrdinalIgnoreCase)
               || key.StartsWith("J", StringComparison.OrdinalIgnoreCase)
               || key.StartsWith("S", StringComparison.OrdinalIgnoreCase)
               || key.StartsWith("H", StringComparison.OrdinalIgnoreCase)
               || key.StartsWith("I", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeLineEndings(string value)
    {
        return value.Replace("\r\n", "\n").Replace('\r', '\n');
    }

    private static string? TryGetValue(IReadOnlyDictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out var value) ? value : null;
    }

    private static IReadOnlyList<string> SplitList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToArray();
    }

    private static int? TryParseInt(string? value)
    {
        return int.TryParse(value, out var parsed) ? parsed : null;
    }
}
