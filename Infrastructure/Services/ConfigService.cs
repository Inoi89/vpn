using System.IO.Compression;
using System.Text;
using System.Text.Json;
using VpnClient.Core.Interfaces;
using VpnClient.Core.Models;

namespace VpnClient.Infrastructure.Services;

public sealed class ConfigService : IConfigService
{
    private readonly string _stateFilePath;

    public ConfigService()
        : this(stateFilePath: null, useCustomStatePath: false)
    {
    }

    public ConfigService(string stateFilePath)
        : this(stateFilePath, useCustomStatePath: true)
    {
    }

    private ConfigService(string? stateFilePath, bool useCustomStatePath)
    {
        _stateFilePath = useCustomStatePath
            ? ResolveStateFilePath(stateFilePath)
            : ResolveStateFilePath(null);
        CurrentProfile = LoadPersistedProfile();
    }

    public ImportedProfile? CurrentProfile { get; private set; }

    public async Task<ImportedProfile> ImportConfigAsync(string path)
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

        var source = await File.ReadAllTextAsync(fullPath);
        var fileName = Path.GetFileName(fullPath);
        var extension = Path.GetExtension(fullPath);

        var profile = string.Equals(extension, ".vpn", StringComparison.OrdinalIgnoreCase)
            ? BuildVpnProfile(fileName, fullPath, source)
            : BuildNativeProfile(fileName, fullPath, source);

        CurrentProfile = profile;
        await PersistProfileAsync(profile);
        return profile;
    }

    public Task<string> LoadConfigAsync()
    {
        if (CurrentProfile is null)
        {
            throw new InvalidOperationException("No imported VPN profile is available.");
        }

        return Task.FromResult(CurrentProfile.RawConfig);
    }

    private ImportedProfile? LoadPersistedProfile()
    {
        if (!File.Exists(_stateFilePath))
        {
            return null;
        }

        try
        {
            var raw = File.ReadAllText(_stateFilePath);
            return JsonSerializer.Deserialize<ImportedProfile>(raw);
        }
        catch
        {
            return null;
        }
    }

    private Task PersistProfileAsync(ImportedProfile profile)
    {
        var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
        return File.WriteAllTextAsync(_stateFilePath, json);
    }

    private static string ResolveStateFilePath(string? stateFilePath)
    {
        if (!string.IsNullOrWhiteSpace(stateFilePath))
        {
            var fullPath = Path.GetFullPath(stateFilePath);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            return fullPath;
        }

        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VpnClient");

        Directory.CreateDirectory(appDataPath);
        return Path.Combine(appDataPath, "profile.json");
    }

    private static ImportedProfile BuildNativeProfile(string fileName, string sourcePath, string source)
    {
        var normalized = NormalizeLineEndings(source).Trim();
        if (!normalized.Contains("[Interface]", StringComparison.OrdinalIgnoreCase)
            || !normalized.Contains("[Peer]", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The imported file is not a valid WireGuard/AmneziaWG config.");
        }

        return CreateProfile(
            fileName,
            sourcePath,
            "AmneziaWG (.conf)",
            normalized);
    }

    private static ImportedProfile BuildVpnProfile(string fileName, string sourcePath, string source)
    {
        var trimmed = source.Trim();
        if (trimmed.StartsWith("vpn://", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed["vpn://".Length..];
        }

        var payload = DecodeBase64Url(trimmed);
        if (payload.Length <= 4)
        {
            throw new InvalidOperationException("The imported .vpn file is malformed.");
        }

        using var compressedStream = new MemoryStream(payload, 4, payload.Length - 4, writable: false);
        using var zlibStream = new ZLibStream(compressedStream, CompressionMode.Decompress);
        using var output = new MemoryStream();
        zlibStream.CopyTo(output);

        using var document = JsonDocument.Parse(output.ToArray());
        var rawConfig = ExtractRawConfig(document.RootElement)
            ?? throw new InvalidOperationException("The imported .vpn file does not contain a usable tunnel config.");

        return CreateProfile(
            fileName,
            sourcePath,
            "Amnezia VPN (.vpn)",
            NormalizeLineEndings(rawConfig).Trim());
    }

    private static ImportedProfile CreateProfile(string fileName, string sourcePath, string format, string rawConfig)
    {
        var displayName = Path.GetFileNameWithoutExtension(fileName);
        var endpoint = ExtractValue(rawConfig, "Endpoint");
        var address = ExtractValue(rawConfig, "Address");
        var primaryDns = ExtractValue(rawConfig, "DNS")?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        return new ImportedProfile(
            string.IsNullOrWhiteSpace(displayName) ? "Imported server" : displayName,
            fileName,
            sourcePath,
            format,
            endpoint,
            address,
            primaryDns,
            DateTimeOffset.UtcNow,
            rawConfig);
    }

    private static string? ExtractRawConfig(JsonElement root)
    {
        if (!root.TryGetProperty("containers", out var containers) || containers.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var container in containers.EnumerateArray())
        {
            foreach (var protocolName in new[] { "awg", "wireguard" })
            {
                if (!container.TryGetProperty(protocolName, out var protocolConfig))
                {
                    continue;
                }

                if (!protocolConfig.TryGetProperty("last_config", out var lastConfigElement)
                    || lastConfigElement.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var lastConfig = lastConfigElement.GetString();
                if (string.IsNullOrWhiteSpace(lastConfig))
                {
                    continue;
                }

                try
                {
                    using var lastConfigDocument = JsonDocument.Parse(lastConfig);
                    if (lastConfigDocument.RootElement.TryGetProperty("config", out var configElement)
                        && configElement.ValueKind == JsonValueKind.String)
                    {
                        return configElement.GetString();
                    }
                }
                catch
                {
                    // Fall through to the raw config path.
                }

                if (lastConfig.Contains("[Interface]", StringComparison.OrdinalIgnoreCase))
                {
                    return lastConfig;
                }
            }
        }

        return null;
    }

    private static string? ExtractValue(string config, string key)
    {
        foreach (var line in config.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith($"{key} =", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parts = trimmed.Split('=', 2, StringSplitOptions.TrimEntries);
            return parts.Length == 2 ? parts[1].Trim() : null;
        }

        return null;
    }

    private static string NormalizeLineEndings(string value)
    {
        return value.Replace("\r\n", "\n").Replace('\r', '\n');
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
}
