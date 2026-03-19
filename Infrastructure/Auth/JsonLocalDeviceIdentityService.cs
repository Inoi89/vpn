using System.Reflection;
using System.Text.Json;
using VpnClient.Core.Interfaces;
using VpnClient.Core.Models.Auth;
using VpnClient.Infrastructure.Updates;

namespace VpnClient.Infrastructure.Auth;

public sealed class JsonLocalDeviceIdentityService : ILocalDeviceIdentityService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _filePath;

    public JsonLocalDeviceIdentityService()
    {
        _filePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "YourVpnClient",
            "device-identity.json");
    }

    public async Task<LocalDeviceIdentity> GetOrCreateAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var existing = await ReadAsync(cancellationToken);
            if (existing is not null)
            {
                return new LocalDeviceIdentity(
                    ResolveDeviceName(),
                    "windows",
                    existing.Fingerprint,
                    ResolveClientVersion());
            }

            var created = new StoredDeviceIdentity(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow);
            await WriteAsync(created, cancellationToken);

            return new LocalDeviceIdentity(
                ResolveDeviceName(),
                "windows",
                created.Fingerprint,
                ResolveClientVersion());
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<StoredDeviceIdentity?> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(_filePath, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<StoredDeviceIdentity>(json, SerializerOptions);
    }

    private async Task WriteAsync(StoredDeviceIdentity identity, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(identity, SerializerOptions);
        await File.WriteAllTextAsync(_filePath, json, cancellationToken);
    }

    private static string ResolveDeviceName()
    {
        return Environment.MachineName;
    }

    private static string ResolveClientVersion()
    {
        return AppVersionParser.GetCurrentVersion(Assembly.GetEntryAssembly() ?? typeof(JsonLocalDeviceIdentityService).Assembly);
    }

    private sealed record StoredDeviceIdentity(string Fingerprint, DateTimeOffset CreatedAtUtc);
}
