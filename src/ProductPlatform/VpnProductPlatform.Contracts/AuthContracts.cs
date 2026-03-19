namespace VpnProductPlatform.Contracts;

public sealed record RegisterAccountRequest(
    string Email,
    string Password,
    string? DisplayName);

public sealed record LoginRequest(
    string Email,
    string Password);

public sealed record RefreshTokenRequest(string RefreshToken);

public sealed record AuthTokenResponse(
    Guid AccountId,
    string Email,
    string DisplayName,
    Guid SessionId,
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc);

public sealed record MeResponse(
    Guid AccountId,
    string Email,
    string DisplayName,
    string Status,
    SubscriptionSummaryResponse? Subscription);
