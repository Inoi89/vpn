namespace VpnClient.Core.Models.Updates;

public sealed record AppUpdateRelease(
    string Version,
    string PackageUrl,
    string Sha256,
    long? SizeBytes,
    DateTimeOffset? PublishedAtUtc,
    string? ReleaseNotes,
    bool IsMandatory,
    string? MinimumSupportedVersion,
    string? Channel,
    string? PackageCertificateThumbprint);
