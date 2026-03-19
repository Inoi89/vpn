using VpnProductPlatform.Application.Abstractions;
using VpnProductPlatform.Contracts;

namespace VpnProductPlatform.Application.Accounts;

public sealed class SessionApplicationService(
    IAccountRepository accountRepository,
    IAccountSessionRepository accountSessionRepository,
    ITokenIssuer tokenIssuer,
    IRefreshTokenService refreshTokenService,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<AuthTokenResponse> RefreshAsync(
        RefreshTokenRequest request,
        AuthSessionContext sessionContext,
        CancellationToken cancellationToken)
    {
        if (!refreshTokenService.TryGetSessionId(request.RefreshToken, out var sessionId))
        {
            throw new InvalidOperationException("Refresh token is invalid.");
        }

        var session = await accountSessionRepository.GetByIdAsync(sessionId, cancellationToken)
            ?? throw new InvalidOperationException("Refresh token is invalid.");

        if (!refreshTokenService.Verify(request.RefreshToken, session.RefreshTokenHash))
        {
            throw new InvalidOperationException("Refresh token is invalid.");
        }

        if (session.RevokedAtUtc is not null)
        {
            throw new InvalidOperationException("Refresh token has been revoked.");
        }

        if (session.IsExpiredAt(clock.UtcNow))
        {
            throw new InvalidOperationException("Refresh token has expired.");
        }

        var account = await accountRepository.GetByIdAsync(session.AccountId, cancellationToken)
            ?? throw new InvalidOperationException("Account was not found.");

        var refreshToken = refreshTokenService.Issue(session.Id);
        session.Rotate(
            refreshToken.TokenHash,
            refreshToken.ExpiresAtUtc,
            sessionContext.IpAddress,
            sessionContext.UserAgent,
            clock.UtcNow);

        var issued = tokenIssuer.Issue(account.Id, account.Email, session.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AuthTokenResponse(
            account.Id,
            account.Email,
            account.DisplayName,
            session.Id,
            issued.Token,
            issued.ExpiresAtUtc,
            refreshToken.Token,
            refreshToken.ExpiresAtUtc);
    }

    public async Task<IReadOnlyList<SessionResponse>> ListAsync(
        Guid accountId,
        Guid currentSessionId,
        CancellationToken cancellationToken)
    {
        var sessions = await accountSessionRepository.ListByAccountIdAsync(accountId, cancellationToken);
        return sessions
            .Select(x => new SessionResponse(
                x.Id,
                GetStatus(x, clock.UtcNow),
                x.IpAddress,
                x.UserAgent,
                x.CreatedAtUtc,
                x.LastSeenAtUtc,
                x.ExpiresAtUtc,
                x.Id == currentSessionId))
            .ToList();
    }

    public async Task RevokeAsync(Guid accountId, Guid sessionId, string reason, CancellationToken cancellationToken)
    {
        var session = await accountSessionRepository.GetByIdAsync(sessionId, cancellationToken)
            ?? throw new InvalidOperationException("Session was not found.");

        if (session.AccountId != accountId)
        {
            throw new InvalidOperationException("Session does not belong to the current account.");
        }

        session.Revoke(reason, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static string GetStatus(Domain.Entities.AccountSession session, DateTimeOffset now)
    {
        if (session.RevokedAtUtc is not null)
        {
            return "Revoked";
        }

        return session.IsExpiredAt(now)
            ? "Expired"
            : "Active";
    }
}
