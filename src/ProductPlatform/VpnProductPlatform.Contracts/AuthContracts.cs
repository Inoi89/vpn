namespace VpnProductPlatform.Contracts;

public sealed record RegisterAccountRequest(
    string Email,
    string Password,
    string? DisplayName);

public sealed record LoginRequest(
    string Email,
    string Password);

public sealed record AuthTokenResponse(
    Guid AccountId,
    string Email,
    string DisplayName,
    string AccessToken,
    DateTimeOffset ExpiresAtUtc);

public sealed record MeResponse(
    Guid AccountId,
    string Email,
    string DisplayName,
    string Status,
    SubscriptionSummaryResponse? Subscription);
