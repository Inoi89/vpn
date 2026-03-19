using VpnProductPlatform.Application.Abstractions;
using VpnProductPlatform.Application.Accounts;
using VpnProductPlatform.Contracts;
using VpnProductPlatform.Domain.Entities;
using VpnProductPlatform.Domain.Enums;

namespace VpnProductPlatform.Tests;

public sealed class AuthSessionFlowTests
{
    [Fact]
    public async Task Register_CreatesSessionAndReturnsRefreshToken()
    {
        var fixture = TestFixture.Create();

        var response = await fixture.Accounts.RegisterAsync(
            new RegisterAccountRequest("alex@example.com", "super-secret", "Alex"),
            new AuthSessionContext("1.1.1.1", "TestAgent/1.0"),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, response.AccountId);
        Assert.NotEqual(Guid.Empty, response.SessionId);
        Assert.StartsWith(response.SessionId.ToString("N"), response.RefreshToken);
        Assert.Single(fixture.AccountSessions.Items);
        Assert.Single(fixture.Subscriptions.Subscriptions);
        Assert.Single(fixture.AccountEmails.Sent);
        Assert.Equal("alex@example.com", fixture.AccountEmails.Sent[0].Email);
    }

    [Fact]
    public async Task Refresh_RotatesRefreshToken()
    {
        var fixture = TestFixture.Create();
        var initial = await fixture.Accounts.RegisterAsync(
            new RegisterAccountRequest("alex@example.com", "super-secret", "Alex"),
            new AuthSessionContext("1.1.1.1", "TestAgent/1.0"),
            CancellationToken.None);

        var refreshed = await fixture.Sessions.RefreshAsync(
            new RefreshTokenRequest(initial.RefreshToken),
            new AuthSessionContext("2.2.2.2", "TestAgent/2.0"),
            CancellationToken.None);

        Assert.NotEqual(initial.RefreshToken, refreshed.RefreshToken);
        Assert.Equal(initial.SessionId, refreshed.SessionId);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Sessions.RefreshAsync(
                new RefreshTokenRequest(initial.RefreshToken),
                new AuthSessionContext("3.3.3.3", "Replay/1.0"),
                CancellationToken.None));

        Assert.Equal("Refresh token is invalid.", error.Message);
    }

    [Fact]
    public async Task List_MarksCurrentSession()
    {
        var fixture = TestFixture.Create();
        var first = await fixture.Accounts.RegisterAsync(
            new RegisterAccountRequest("alex@example.com", "super-secret", "Alex"),
            new AuthSessionContext("1.1.1.1", "Primary/1.0"),
            CancellationToken.None);

        await fixture.Accounts.LoginAsync(
            new LoginRequest("alex@example.com", "super-secret"),
            new AuthSessionContext("2.2.2.2", "Secondary/1.0"),
            CancellationToken.None);

        var sessions = await fixture.Sessions.ListAsync(first.AccountId, first.SessionId, CancellationToken.None);

        Assert.Equal(2, sessions.Count);
        Assert.Single(sessions.Where(x => x.IsCurrent));
        Assert.Equal(first.SessionId, sessions.Single(x => x.IsCurrent).SessionId);
    }

    [Fact]
    public async Task Revoke_BlocksFutureRefresh()
    {
        var fixture = TestFixture.Create();
        var auth = await fixture.Accounts.RegisterAsync(
            new RegisterAccountRequest("alex@example.com", "super-secret", "Alex"),
            new AuthSessionContext("1.1.1.1", "TestAgent/1.0"),
            CancellationToken.None);

        await fixture.Sessions.RevokeAsync(auth.AccountId, auth.SessionId, "Manual revoke", CancellationToken.None);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Sessions.RefreshAsync(
                new RefreshTokenRequest(auth.RefreshToken),
                new AuthSessionContext("2.2.2.2", "TestAgent/2.0"),
                CancellationToken.None));

        Assert.Equal("Refresh token has been revoked.", error.Message);
    }

    private sealed class TestFixture
    {
        private TestFixture(
            AccountApplicationService accounts,
            SessionApplicationService sessions,
            InMemoryAccountSessionRepository accountSessions,
            InMemorySubscriptionRepository subscriptions,
            FakeAccountEmailService accountEmails)
        {
            Accounts = accounts;
            Sessions = sessions;
            AccountSessions = accountSessions;
            Subscriptions = subscriptions;
            AccountEmails = accountEmails;
        }

        public AccountApplicationService Accounts { get; }
        public SessionApplicationService Sessions { get; }
        public InMemoryAccountSessionRepository AccountSessions { get; }
        public InMemorySubscriptionRepository Subscriptions { get; }
        public FakeAccountEmailService AccountEmails { get; }

        public static TestFixture Create()
        {
            var clock = new FakeClock(new DateTimeOffset(2026, 3, 19, 9, 0, 0, TimeSpan.Zero));
            var accounts = new InMemoryAccountRepository();
            var sessions = new InMemoryAccountSessionRepository();
            var subscriptions = new InMemorySubscriptionRepository(clock.UtcNow);
            var passwordHasher = new FakePasswordHashService();
            var tokenIssuer = new FakeTokenIssuer();
            var refreshTokens = new FakeRefreshTokenService(clock);
            var accountEmails = new FakeAccountEmailService();
            var unitOfWork = new FakeUnitOfWork();

            return new TestFixture(
                new AccountApplicationService(
                    accounts,
                    sessions,
                    subscriptions,
                    passwordHasher,
                    tokenIssuer,
                    refreshTokens,
                    accountEmails,
                    unitOfWork,
                    clock),
                new SessionApplicationService(
                    accounts,
                    sessions,
                    tokenIssuer,
                    refreshTokens,
                    unitOfWork,
                    clock),
                sessions,
                subscriptions,
                accountEmails);
        }
    }

    private sealed class FakeAccountEmailService : IAccountEmailService
    {
        public List<(string Email, string DisplayName, string? PlanName, DateTimeOffset? SubscriptionEndsAtUtc)> Sent { get; } = [];

        public Task SendWelcomeAsync(
            string email,
            string displayName,
            string? planName,
            DateTimeOffset? subscriptionEndsAtUtc,
            CancellationToken cancellationToken)
        {
            Sent.Add((email, displayName, planName, subscriptionEndsAtUtc));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; private set; } = utcNow;
    }

    private sealed class FakePasswordHashService : IPasswordHashService
    {
        public string HashPassword(string subject, string password) => $"{subject}:{password}";
        public bool VerifyHashedPassword(string subject, string passwordHash, string password) => passwordHash == HashPassword(subject, password);
    }

    private sealed class FakeTokenIssuer : ITokenIssuer
    {
        public (string Token, DateTimeOffset ExpiresAtUtc) Issue(Guid accountId, string email, Guid sessionId)
        {
            return ($"access-{accountId:N}-{sessionId:N}", new DateTimeOffset(2026, 4, 19, 9, 0, 0, TimeSpan.Zero));
        }
    }

    private sealed class FakeRefreshTokenService(FakeClock clock) : IRefreshTokenService
    {
        private int _sequence;

        public RefreshTokenEnvelope Issue(Guid sessionId)
        {
            var secret = $"secret-{++_sequence}";
            return new RefreshTokenEnvelope(
                $"{sessionId:N}.{secret}",
                secret,
                clock.UtcNow.AddDays(30));
        }

        public bool TryGetSessionId(string refreshToken, out Guid sessionId)
        {
            sessionId = Guid.Empty;
            var parts = refreshToken.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 2 && Guid.TryParseExact(parts[0], "N", out sessionId);
        }

        public bool Verify(string refreshToken, string expectedTokenHash)
        {
            var parts = refreshToken.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 2 && parts[1] == expectedTokenHash;
        }
    }

    private sealed class InMemoryAccountRepository : IAccountRepository
    {
        private readonly List<Account> _items = [];

        public Task AddAsync(Account account, CancellationToken cancellationToken)
        {
            _items.Add(account);
            return Task.CompletedTask;
        }

        public Task<Account?> FindByEmailAsync(string email, CancellationToken cancellationToken)
        {
            return Task.FromResult(_items.FirstOrDefault(x => x.Email == email.Trim().ToLowerInvariant()));
        }

        public Task<Account?> GetByIdAsync(Guid accountId, CancellationToken cancellationToken)
        {
            return Task.FromResult(_items.FirstOrDefault(x => x.Id == accountId));
        }
    }

    private sealed class InMemoryAccountSessionRepository : IAccountSessionRepository
    {
        public List<AccountSession> Items { get; } = [];

        public Task AddAsync(AccountSession session, CancellationToken cancellationToken)
        {
            Items.Add(session);
            return Task.CompletedTask;
        }

        public Task<AccountSession?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Items.FirstOrDefault(x => x.Id == sessionId));
        }

        public Task<IReadOnlyList<AccountSession>> ListByAccountIdAsync(Guid accountId, CancellationToken cancellationToken)
        {
            IReadOnlyList<AccountSession> sessions = Items.Where(x => x.AccountId == accountId).OrderByDescending(x => x.LastSeenAtUtc).ToList();
            return Task.FromResult(sessions);
        }
    }

    private sealed class InMemorySubscriptionRepository : ISubscriptionRepository
    {
        private readonly SubscriptionPlan _defaultPlan;

        public InMemorySubscriptionRepository(DateTimeOffset now)
        {
            _defaultPlan = SubscriptionPlan.Create(
                Guid.NewGuid(),
                "Trial",
                maxDevices: 2,
                maxConcurrentSessions: 2,
                priceAmount: 0m,
                currency: "USD",
                billingPeriodMonths: 1,
                isActive: true,
                now);
        }

        public List<Subscription> Subscriptions { get; } = [];

        public Task AddAsync(Subscription subscription, CancellationToken cancellationToken)
        {
            Subscriptions.Add(subscription);
            return Task.CompletedTask;
        }

        public Task<Subscription?> GetActiveByAccountIdAsync(Guid accountId, DateTimeOffset now, CancellationToken cancellationToken)
        {
            return Task.FromResult(Subscriptions.FirstOrDefault(x => x.AccountId == accountId && x.IsActiveAt(now)));
        }

        public Task<SubscriptionPlan?> GetDefaultPlanAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<SubscriptionPlan?>(_defaultPlan);
        }

        public Task AddPlanAsync(SubscriptionPlan plan, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
