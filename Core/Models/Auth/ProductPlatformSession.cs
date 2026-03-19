namespace VpnClient.Core.Models.Auth;

public sealed record ProductPlatformSession(
    Guid AccountId,
    string Email,
    string DisplayName,
    Guid SessionId,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc)
{
    public bool IsAccessTokenExpired(DateTimeOffset now, TimeSpan? skew = null)
    {
        var tolerance = skew ?? TimeSpan.FromMinutes(1);
        return AccessTokenExpiresAtUtc <= now.Add(tolerance);
    }

    public bool IsRefreshTokenExpired(DateTimeOffset now, TimeSpan? skew = null)
    {
        var tolerance = skew ?? TimeSpan.FromMinutes(1);
        return RefreshTokenExpiresAtUtc <= now.Add(tolerance);
    }
}
