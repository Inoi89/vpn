using System.Text.Json.Serialization;

namespace VpnClient.Infrastructure.Updates;

internal sealed class UpdateManifestDocument
{
    [JsonPropertyName("applicationId")]
    public string? ApplicationId { get; init; }

    [JsonPropertyName("channel")]
    public string? Channel { get; init; }

    [JsonPropertyName("release")]
    public UpdateReleaseDocument? Release { get; init; }
}

internal sealed class UpdateReleaseDocument
{
    [JsonPropertyName("version")]
    public string? Version { get; init; }

    [JsonPropertyName("packageUrl")]
    public string? PackageUrl { get; init; }

    [JsonPropertyName("sha256")]
    public string? Sha256 { get; init; }

    [JsonPropertyName("sizeBytes")]
    public long? SizeBytes { get; init; }

    [JsonPropertyName("publishedAtUtc")]
    public DateTimeOffset? PublishedAtUtc { get; init; }

    [JsonPropertyName("releaseNotes")]
    public string? ReleaseNotes { get; init; }

    [JsonPropertyName("isMandatory")]
    public bool IsMandatory { get; init; }

    [JsonPropertyName("minimumSupportedVersion")]
    public string? MinimumSupportedVersion { get; init; }

    [JsonPropertyName("channel")]
    public string? Channel { get; init; }

    [JsonPropertyName("packageCertificateThumbprint")]
    public string? PackageCertificateThumbprint { get; init; }
}
