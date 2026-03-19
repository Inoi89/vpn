using VpnProductPlatform.Application.Abstractions;
using VpnProductPlatform.Contracts;
using VpnProductPlatform.Domain.Entities;
using VpnProductPlatform.Domain.Enums;

namespace VpnProductPlatform.Application.Accounts;

public sealed class AccountApplicationService(
    IAccountRepository accountRepository,
    IAccountSessionRepository accountSessionRepository,
    ISubscriptionRepository subscriptionRepository,
    IPasswordHashService passwordHashService,
    ITokenIssuer tokenIssuer,
    IRefreshTokenService refreshTokenService,
    IEmailVerificationTokenService emailVerificationTokenService,
    IAccountEmailService accountEmailService,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<AuthTokenResponse> RegisterAsync(
        RegisterAccountRequest request,
        AuthSessionContext sessionContext,
        CancellationToken cancellationToken)
    {
        ValidatePassword(request.Password);

        var email = NormalizeEmail(request.Email);
        var existing = await accountRepository.FindByEmailAsync(email, cancellationToken);
        if (existing is not null)
        {
            throw new InvalidOperationException("Account with this email already exists.");
        }

        var displayName = string.IsNullOrWhiteSpace(request.DisplayName)
            ? email.Split('@', 2)[0]
            : request.DisplayName.Trim();

        var passwordHash = passwordHashService.HashPassword(email, request.Password);
        var account = Account.Create(Guid.NewGuid(), email, displayName, passwordHash, clock.UtcNow);
        await accountRepository.AddAsync(account, cancellationToken);

        var response = await CreateAuthResponseAsync(account, sessionContext, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var verification = emailVerificationTokenService.Issue(account.Id, account.Email);
        await accountEmailService.SendVerificationAsync(
            account.Email,
            account.DisplayName,
            verification.Token,
            cancellationToken);

        return response;
    }

    public async Task<AuthTokenResponse> LoginAsync(
        LoginRequest request,
        AuthSessionContext sessionContext,
        CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var account = await accountRepository.FindByEmailAsync(email, cancellationToken)
            ?? throw new InvalidOperationException("Invalid credentials.");

        if (account.Status is AccountStatus.Suspended or AccountStatus.Deleted)
        {
            throw new InvalidOperationException("Account is unavailable.");
        }

        if (!passwordHashService.VerifyHashedPassword(email, account.PasswordHash, request.Password))
        {
            throw new InvalidOperationException("Invalid credentials.");
        }

        account.RecordLogin(clock.UtcNow);
        var response = await CreateAuthResponseAsync(account, sessionContext, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return response;
    }

    public async Task<MeResponse> GetCurrentAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var account = await accountRepository.GetByIdAsync(accountId, cancellationToken)
            ?? throw new InvalidOperationException("Account was not found.");

        var subscription = await subscriptionRepository.GetActiveByAccountIdAsync(accountId, clock.UtcNow, cancellationToken);
        return new MeResponse(
            account.Id,
            account.Email,
            account.DisplayName,
            account.Status.ToString(),
            account.Status == AccountStatus.Active,
            subscription is null
                ? null
                : new SubscriptionSummaryResponse(
                    subscription.Id,
                    subscription.Plan.Name,
                    subscription.Status.ToString(),
                    subscription.Plan.MaxDevices,
                    subscription.Plan.MaxConcurrentSessions,
                    subscription.StartsAtUtc,
                    subscription.EndsAtUtc));
    }

    public async Task<VerifyEmailResponse> VerifyEmailAsync(VerifyEmailRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            throw new InvalidOperationException("Verification token is required.");
        }

        if (!emailVerificationTokenService.TryValidate(request.Token.Trim(), out var payload) || payload is null)
        {
            throw new InvalidOperationException("Verification token is invalid or expired.");
        }

        var account = await accountRepository.GetByIdAsync(payload.AccountId, cancellationToken)
            ?? throw new InvalidOperationException("Account was not found.");

        if (!string.Equals(account.Email, payload.Email, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Verification token is invalid.");
        }

        var alreadyVerified = account.Status == AccountStatus.Active;
        if (!alreadyVerified)
        {
            account.VerifyEmail(clock.UtcNow);
            await EnsureTrialSubscriptionAsync(account.Id, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new VerifyEmailResponse(
            account.Id,
            account.Email,
            account.Status.ToString(),
            account.Status == AccountStatus.Active,
            alreadyVerified
                ? "Email is already verified."
                : "Email verification completed.");
    }

    public async Task ResendVerificationEmailAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var account = await accountRepository.GetByIdAsync(accountId, cancellationToken)
            ?? throw new InvalidOperationException("Account was not found.");

        if (account.Status == AccountStatus.Active)
        {
            return;
        }

        var verification = emailVerificationTokenService.Issue(account.Id, account.Email);
        await accountEmailService.SendVerificationAsync(
            account.Email,
            account.DisplayName,
            verification.Token,
            cancellationToken);
    }

    private async Task EnsureTrialSubscriptionAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var current = await subscriptionRepository.GetActiveByAccountIdAsync(accountId, clock.UtcNow, cancellationToken);
        if (current is not null)
        {
            return;
        }

        var defaultPlan = await subscriptionRepository.GetDefaultPlanAsync(cancellationToken)
            ?? throw new InvalidOperationException("No default subscription plan is configured.");

        var trial = Subscription.CreateTrial(
            Guid.NewGuid(),
            accountId,
            defaultPlan.Id,
            clock.UtcNow,
            TimeSpan.FromDays(7));
        await subscriptionRepository.AddAsync(trial, cancellationToken);
    }

    private async Task<AuthTokenResponse> CreateAuthResponseAsync(
        Account account,
        AuthSessionContext sessionContext,
        CancellationToken cancellationToken)
    {
        var sessionId = Guid.NewGuid();
        var refreshToken = refreshTokenService.Issue(sessionId);
        var session = AccountSession.Create(
            sessionId,
            account.Id,
            refreshToken.TokenHash,
            refreshToken.ExpiresAtUtc,
            sessionContext.IpAddress,
            sessionContext.UserAgent,
            clock.UtcNow);

        await accountSessionRepository.AddAsync(session, cancellationToken);

        var issued = tokenIssuer.Issue(account.Id, account.Email, session.Id);
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

    private static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException("Email is required.");
        }

        return email.Trim().ToLowerInvariant();
    }

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Trim().Length < 8)
        {
            throw new InvalidOperationException("Password must contain at least 8 characters.");
        }
    }
}
