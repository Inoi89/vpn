using VpnClient.Core.Models;
using VpnClient.Infrastructure.Import;

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

        var tunnelName = BuildTunnelName(profile);
        var configPath = Path.Combine(_configDirectory, $"{tunnelName}.conf");
        return new PreparedTunnelProfile(profile.Id, profile.DisplayName, tunnelName, configPath);
    }

    public async Task<PreparedTunnelProfile> PrepareAsync(ImportedServerProfile profile, CancellationToken cancellationToken = default)
    {
        var preparedProfile = Describe(profile);

        Directory.CreateDirectory(_configDirectory);

        var rawConfig = NormalizeConfig(BuildRuntimeConfig(profile));
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

    private static string BuildRuntimeConfig(ImportedServerProfile profile)
    {
        if (profile.SourceFormat == TunnelConfigFormat.AmneziaVpn
            && !string.IsNullOrWhiteSpace(profile.RawPackageJson))
        {
            return AmneziaVpnConfigMaterializer.Materialize(profile.RawPackageJson, profile.TunnelConfig.RawConfig);
        }

        return profile.TunnelConfig.RawConfig;
    }

    private static string BuildTunnelName(ImportedServerProfile profile)
    {
        var slug = new string(profile.DisplayName
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
            .ToArray());

        while (slug.Contains("__", StringComparison.Ordinal))
        {
            slug = slug.Replace("__", "_", StringComparison.Ordinal);
        }

        slug = slug.Trim('_');
        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = "server";
        }

        if (slug.Length > 18)
        {
            slug = slug[..18].TrimEnd('_');
        }

        var suffix = profile.Id.ToString("N")[..6];
        return $"vpn_{slug}_{suffix}";
    }
}
