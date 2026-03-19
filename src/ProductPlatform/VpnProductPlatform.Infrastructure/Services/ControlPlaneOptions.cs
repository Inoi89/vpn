namespace VpnProductPlatform.Infrastructure.Services;

public sealed class ControlPlaneOptions
{
    public const string SectionName = "ControlPlane";

    public string BaseUrl { get; init; } = string.Empty;

    public string JwtSigningKey { get; init; } = string.Empty;

    public string JwtIssuer { get; init; } = "vpn-control-plane";

    public string JwtAudience { get; init; } = "vpn-control-plane-ui";

    public string JwtSubject { get; init; } = "product-platform";

    public string JwtRole { get; init; } = "internal";
}
