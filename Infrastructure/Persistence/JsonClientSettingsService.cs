using System.Text.Json;
using VpnClient.Core.Interfaces;
using VpnClient.Core.Models;

namespace VpnClient.Infrastructure.Persistence;

public sealed class JsonClientSettingsService : IClientSettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _filePath;

    public JsonClientSettingsService()
        : this(null)
    {
    }

    public JsonClientSettingsService(string? filePath)
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
            "client-settings.json");
    }

    public async Task<ClientSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_filePath))
            {
                return new ClientSettings();
            }

            var json = await File.ReadAllTextAsync(_filePath, cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new ClientSettings();
            }

            return JsonSerializer.Deserialize<ClientSettings>(json, SerializerOptions) ?? new ClientSettings();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(ClientSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            EnsureDirectory();
            var json = JsonSerializer.Serialize(settings, SerializerOptions);
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
        finally
        {
            _gate.Release();
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
}
