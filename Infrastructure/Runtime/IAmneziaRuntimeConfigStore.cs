using VpnClient.Core.Models;

namespace VpnClient.Infrastructure.Runtime;

public interface IAmneziaRuntimeConfigStore
{
    PreparedTunnelProfile Describe(ImportedServerProfile profile);

    Task<PreparedTunnelProfile> PrepareAsync(ImportedServerProfile profile, CancellationToken cancellationToken = default);

    Task DeleteAsync(PreparedTunnelProfile preparedProfile, CancellationToken cancellationToken = default);
}

public sealed class ProgramDataAmneziaRuntimeConfigStore : IAmneziaRuntimeConfigStore
{
    private readonly string _configDirectory;

    public ProgramDataAmneziaRuntimeConfigStore()
        : this(GetDefaultConfigDirectory())
    {
    }

    public ProgramDataAmneziaRuntimeConfigStore(string configDirectory)
    {
        if (string.IsNullOrWhiteSpace(configDirectory))
        {
            throw new ArgumentException("Config directory is required.", nameof(configDirectory));
        }

        _configDirectory = Path.GetFullPath(configDirectory);
    }

    public static string GetDefaultConfigDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "YourVpnClient",
            "Runtime",
            "Configurations");
    }

    public PreparedTunnelProfile Describe(ImportedServerProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var tunnelName = $"yvc_{profile.Id:N}"[..16];
        var configPath = Path.Combine(_configDirectory, $"{tunnelName}.conf");
        return new PreparedTunnelProfile(profile.Id, profile.DisplayName, tunnelName, configPath);
    }

    public async Task<PreparedTunnelProfile> PrepareAsync(ImportedServerProfile profile, CancellationToken cancellationToken = default)
    {
        var preparedProfile = Describe(profile);

        Directory.CreateDirectory(_configDirectory);

        var rawConfig = NormalizeConfig(profile.TunnelConfig.RawConfig);
        await File.WriteAllTextAsync(preparedProfile.ConfigPath, rawConfig, cancellationToken);
        return preparedProfile;
    }

    public Task DeleteAsync(PreparedTunnelProfile preparedProfile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preparedProfile);

        if (File.Exists(preparedProfile.ConfigPath))
        {
            File.Delete(preparedProfile.ConfigPath);
        }

        return Task.CompletedTask;
    }

    private static string NormalizeConfig(string rawConfig)
    {
        var normalized = rawConfig
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();

        return normalized + Environment.NewLine;
    }
}
