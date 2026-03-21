namespace VpnClient.Infrastructure.Auth;

public sealed class ProductPlatformOptions
{
    public string ApiBaseUrl { get; init; } = "https://etovpn.com/api/";

    public string CabinetUrl { get; init; } = "https://etovpn.com/";
}
