using System.Text.Json.Serialization;

namespace VpnClient.Core.Models;

public sealed record ImportedServerProfile(
    Guid Id,
    string DisplayName,
    ImportedTunnelConfig ImportedConfig,
    DateTimeOffset ImportedAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    [JsonIgnore]
    public TunnelConfig TunnelConfig => ImportedConfig.TunnelConfig;

    [JsonIgnore]
    public TunnelConfigFormat SourceFormat => ImportedConfig.SourceFormat;

    [JsonIgnore]
    public string SourceFileName => ImportedConfig.FileName;

    [JsonIgnore]
    public string SourcePath => ImportedConfig.SourcePath;

    [JsonIgnore]
    public string RawSource => ImportedConfig.RawSource;

    [JsonIgnore]
    public string? RawPackageJson => ImportedConfig.RawPackageJson;

    [JsonIgnore]
    public string? Endpoint => ImportedConfig.TunnelConfig.Endpoint;

    [JsonIgnore]
    public string? Address => ImportedConfig.TunnelConfig.Address;

    [JsonIgnore]
    public IReadOnlyList<string> DnsServers => ImportedConfig.TunnelConfig.DnsServers;

    [JsonIgnore]
    public string? Mtu => ImportedConfig.TunnelConfig.Mtu;

    [JsonIgnore]
    public IReadOnlyList<string> AllowedIps => ImportedConfig.TunnelConfig.AllowedIps;

    [JsonIgnore]
    public string? PublicKey => ImportedConfig.TunnelConfig.PublicKey;

    [JsonIgnore]
    public string? PresharedKey => ImportedConfig.TunnelConfig.PresharedKey;

    [JsonIgnore]
    public bool HasAwgMetadata => ImportedConfig.TunnelConfig.AwgValues.Count > 0;
}
