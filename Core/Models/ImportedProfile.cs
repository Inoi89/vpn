namespace VpnClient.Core.Models;

public sealed record ImportedProfile(
    string DisplayName,
    string FileName,
    string SourcePath,
    string Format,
    string? Endpoint,
    string? Address,
    string? PrimaryDns,
    DateTimeOffset ImportedAtUtc,
    string RawConfig);
