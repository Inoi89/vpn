using System.Text.Json;
using VpnClient.Core.Interfaces;
using VpnClient.Core.Models;

namespace VpnClient.Infrastructure.Persistence;

public sealed class JsonProfileRepository : IProfileRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _filePath;

    public JsonProfileRepository()
        : this(null)
    {
    }

    public JsonProfileRepository(string? filePath)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath)
            ? GetDefaultStoragePath()
            : Path.GetFullPath(filePath);
        EnsureDirectory();
    }

    public static string GetDefaultStoragePath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "YourVpnClient",
            "profiles.json");
    }

    public async Task<ProfileCollectionState> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await ReadStateAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ProfileCollectionState> AddAsync(ImportedServerProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ValidateDisplayName(profile.DisplayName);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var state = await ReadStateAsync(cancellationToken);
            if (state.Profiles.Any(existing => existing.Id == profile.Id))
            {
                throw new InvalidOperationException($"Profile '{profile.Id}' already exists.");
            }

            var storedProfile = NormalizeProfile(profile);
            var updatedProfiles = state.Profiles.Append(storedProfile).ToList();
            var activeProfileId = state.ActiveProfileId ?? storedProfile.Id;

            var updated = new ProfileCollectionState(activeProfileId, updatedProfiles);
            await WriteStateAsync(updated, cancellationToken);
            return updated;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ProfileCollectionState> DeleteAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var state = await ReadStateAsync(cancellationToken);
            var removedProfile = state.Profiles.FirstOrDefault(profile => profile.Id == profileId);
            if (removedProfile is null)
            {
                return state;
            }

            var updatedProfiles = state.Profiles
                .Where(profile => profile.Id != profileId)
                .ToList();

            var activeProfileId = state.ActiveProfileId == profileId
                ? null
                : state.ActiveProfileId;

            if (activeProfileId.HasValue && !updatedProfiles.Any(profile => profile.Id == activeProfileId.Value))
            {
                activeProfileId = null;
            }

            var updated = new ProfileCollectionState(activeProfileId, updatedProfiles);
            await WriteStateAsync(updated, cancellationToken);
            return updated;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ProfileCollectionState> RenameAsync(Guid profileId, string displayName, CancellationToken cancellationToken = default)
    {
        ValidateDisplayName(displayName);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var state = await ReadStateAsync(cancellationToken);
            var index = state.Profiles.FindIndex(profile => profile.Id == profileId);
            if (index < 0)
            {
                throw new KeyNotFoundException($"Profile '{profileId}' was not found.");
            }

            var updatedProfiles = state.Profiles.ToList();
            var target = updatedProfiles[index];
            updatedProfiles[index] = target with
            {
                DisplayName = displayName.Trim(),
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };

            var updated = new ProfileCollectionState(state.ActiveProfileId, updatedProfiles);
            await WriteStateAsync(updated, cancellationToken);
            return updated;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ProfileCollectionState> SetActiveAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var state = await ReadStateAsync(cancellationToken);
            if (!state.Profiles.Any(profile => profile.Id == profileId))
            {
                throw new KeyNotFoundException($"Profile '{profileId}' was not found.");
            }

            if (state.ActiveProfileId == profileId)
            {
                return state;
            }

            var updated = new ProfileCollectionState(profileId, state.Profiles.ToList());
            await WriteStateAsync(updated, cancellationToken);
            return updated;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<ProfileCollectionState> ReadStateAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return new ProfileCollectionState(null, []);
        }

        var json = await File.ReadAllTextAsync(_filePath, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ProfileCollectionState(null, []);
        }

        var state = JsonSerializer.Deserialize<ProfileCollectionState>(json, SerializerOptions);
        if (state is null)
        {
            return new ProfileCollectionState(null, []);
        }

        return NormalizeState(state);
    }

    private async Task WriteStateAsync(ProfileCollectionState state, CancellationToken cancellationToken)
    {
        EnsureDirectory();

        var normalized = NormalizeState(state);
        var json = JsonSerializer.Serialize(normalized, SerializerOptions);
        var tempPath = $"{_filePath}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(tempPath, json, cancellationToken);

        if (File.Exists(_filePath))
        {
            File.Replace(tempPath, _filePath, destinationBackupFileName: null);
        }
        else
        {
            File.Move(tempPath, _filePath);
        }
    }

    private void EnsureDirectory()
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static ProfileCollectionState NormalizeState(ProfileCollectionState state)
    {
        var profiles = state.Profiles
            .Select(NormalizeProfile)
            .OrderBy(profile => profile.ImportedAtUtc)
            .ToList();

        var activeProfileId = state.ActiveProfileId;
        if (activeProfileId.HasValue && !profiles.Any(profile => profile.Id == activeProfileId.Value))
        {
            activeProfileId = null;
        }

        if (!activeProfileId.HasValue && profiles.Count == 1)
        {
            activeProfileId = profiles[0].Id;
        }

        return new ProfileCollectionState(activeProfileId, profiles);
    }

    private static ImportedServerProfile NormalizeProfile(ImportedServerProfile profile)
    {
        var importedConfig = profile.ImportedConfig with
        {
            DisplayName = string.IsNullOrWhiteSpace(profile.ImportedConfig.DisplayName)
                ? profile.DisplayName.Trim()
                : profile.ImportedConfig.DisplayName.Trim(),
            FileName = profile.ImportedConfig.FileName.Trim(),
            SourcePath = profile.ImportedConfig.SourcePath.Trim(),
            RawSource = profile.ImportedConfig.RawSource.TrimEnd(),
            RawPackageJson = string.IsNullOrWhiteSpace(profile.ImportedConfig.RawPackageJson)
                ? null
                : profile.ImportedConfig.RawPackageJson.Trim(),
            TunnelConfig = NormalizeTunnelConfig(profile.ImportedConfig.TunnelConfig)
        };

        return profile with
        {
            DisplayName = profile.DisplayName.Trim(),
            ImportedConfig = importedConfig,
            UpdatedAtUtc = profile.UpdatedAtUtc == default ? profile.ImportedAtUtc : profile.UpdatedAtUtc
        };
    }

    private static TunnelConfig NormalizeTunnelConfig(TunnelConfig config)
    {
        var interfaceValues = config.InterfaceValues
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
            .ToDictionary(pair => pair.Key.Trim(), pair => pair.Value.Trim(), StringComparer.OrdinalIgnoreCase);

        var peerValues = config.PeerValues
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
            .ToDictionary(pair => pair.Key.Trim(), pair => pair.Value.Trim(), StringComparer.OrdinalIgnoreCase);

        var awgValues = config.AwgValues
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
            .ToDictionary(pair => pair.Key.Trim(), pair => pair.Value.Trim(), StringComparer.OrdinalIgnoreCase);

        return config with
        {
            RawConfig = config.RawConfig.TrimEnd(),
            InterfaceValues = interfaceValues,
            PeerValues = peerValues,
            AwgValues = awgValues,
            Address = string.IsNullOrWhiteSpace(config.Address) ? null : config.Address.Trim(),
            DnsServers = config.DnsServers
                .Where(dns => !string.IsNullOrWhiteSpace(dns))
                .Select(dns => dns.Trim())
                .ToArray(),
            Mtu = string.IsNullOrWhiteSpace(config.Mtu) ? null : config.Mtu.Trim(),
            AllowedIps = config.AllowedIps
                .Where(address => !string.IsNullOrWhiteSpace(address))
                .Select(address => address.Trim())
                .ToArray(),
            Endpoint = string.IsNullOrWhiteSpace(config.Endpoint) ? null : config.Endpoint.Trim(),
            PublicKey = string.IsNullOrWhiteSpace(config.PublicKey) ? null : config.PublicKey.Trim(),
            PresharedKey = string.IsNullOrWhiteSpace(config.PresharedKey) ? null : config.PresharedKey.Trim()
        };
    }

    private static void ValidateDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name is required.", nameof(displayName));
        }
    }
}
