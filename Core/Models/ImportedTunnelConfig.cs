namespace VpnClient.Core.Models;

public sealed record ImportedTunnelConfig(
    string DisplayName,
    string FileName,
    string SourcePath,
    TunnelConfigFormat SourceFormat,
    DateTimeOffset ImportedAtUtc,
    string RawSource,
    string? RawPackageJson,
    TunnelConfig TunnelConfig);
