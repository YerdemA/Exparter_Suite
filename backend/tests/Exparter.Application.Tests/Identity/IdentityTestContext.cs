using System.Reflection;
using Exparter.Application.Common;
using Exparter.Application.Identity;
using Exparter.Domain.Identity;

namespace Exparter.Application.Tests.Identity;

/// <summary>In-memory stand-ins for everything the Identity handlers depend on.</summary>
internal sealed class IdentityTestContext
{
    public const string Password = "Passw0rd!x";

    public TestClock Clock { get; } = new();
    public InMemoryUsers Users { get; } = new();
    public InMemoryRefreshTokens RefreshTokens { get; } = new();
    public InMemoryOneTimeTokens OneTimeTokens { get; } = new();
    public FakePasswordHasher Hasher { get; } = new();
    public FakeTokenService Tokens { get; } = new();
    public RecordingEmailSender Emails { get; } = new();
    public CountingUnitOfWork UnitOfWork { get; } = new();

    public TokenIssuer TokenIssuer => new(Tokens, RefreshTokens, Clock);

    public User AddUser(string email = "ayse@example.com", string username = "ayse")
    {
        var user = User.Register(email, username, Hasher.Hash(Password), ageConfirmed: true, Clock.GetUtcNow());
        Users.Add(user);
        return user;
    }

    /// <summary>Issues a refresh token as a sign-in would and returns the value the client holds.</summary>
    public string AddRefreshToken(User user)
    {
        var opaque = Tokens.GenerateOpaqueToken();
        RefreshTokens.Add(RefreshToken.IssueForSignIn(user.Id, opaque.Hash, Clock.GetUtcNow()));
        return opaque.Value;
    }

    public string AddOneTimeToken(User user, OneTimeTokenPurpose purpose)
    {
        var opaque = Tokens.GenerateOpaqueToken();
        OneTimeTokens.Add(OneTimeToken.Issue(user.Id, purpose, opaque.Hash, Clock.GetUtcNow()));
        return opaque.Value;
    }

    /// <summary>
    /// Suspension has no use case yet (an operator sets it in the database), so tests
    /// reach the state the same way: from outside the domain model.
    /// </summary>
    public static void Suspend(User user) =>
        typeof(User).GetProperty(nameof(User.Status), BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(user, UserStatus.Suspended);
}

internal sealed class TestClock : TimeProvider
{
    private DateTimeOffset _now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan duration) => _now += duration;
}

internal sealed class InMemoryUsers : IUserRepository
{
    public List<User> Items { get; } = [];

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(x => x.Id == id));

    public Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(x => x.Email == normalizedEmail));

    public Task<User?> GetByUsernameAsync(string normalizedUsername, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(x => x.Username == normalizedUsername));

    public void Add(User user) => Items.Add(user);
}

internal sealed class InMemoryRefreshTokens : IRefreshTokenRepository
{
    public List<RefreshToken> Items { get; } = [];

    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(x => x.TokenHash == tokenHash));

    public Task<IReadOnlyList<RefreshToken>> ListByFamilyAsync(Guid familyId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RefreshToken>>(Items.Where(x => x.FamilyId == familyId).ToList());

    public Task<IReadOnlyList<RefreshToken>> ListActiveByUserAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RefreshToken>>(Items.Where(x => x.UserId == userId && x.IsActive(now)).ToList());

    public void Add(RefreshToken token) => Items.Add(token);
}

internal sealed class InMemoryOneTimeTokens : IOneTimeTokenRepository
{
    public List<OneTimeToken> Items { get; } = [];

    public Task<OneTimeToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(x => x.TokenHash == tokenHash));

    public Task<IReadOnlyList<OneTimeToken>> ListUnusedAsync(
        Guid userId,
        OneTimeTokenPurpose purpose,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<OneTimeToken>>(
            Items.Where(x => x.UserId == userId && x.Purpose == purpose && x.UsedAt is null).ToList());

    public void Add(OneTimeToken token) => Items.Add(token);
}

internal sealed class FakePasswordHasher : IPasswordHasher
{
    public string Hash(string password) => $"hashed:{password}";

    public bool Verify(string passwordHash, string password) => passwordHash == Hash(password);
}

internal sealed class FakeTokenService : ITokenService
{
    private int _counter;

    public AccessToken CreateAccessToken(User user) => new($"access:{user.Id}", 900);

    public OpaqueToken GenerateOpaqueToken()
    {
        var value = $"opaque-{++_counter}";
        return new OpaqueToken(value, HashOpaqueToken(value));
    }

    public string HashOpaqueToken(string value) => $"hash:{value}";
}

internal sealed class RecordingEmailSender : IEmailSender
{
    public List<(string Email, string Token)> Verifications { get; } = [];
    public List<(string Email, string Token)> PasswordResets { get; } = [];

    public Task SendEmailVerificationAsync(string email, string token, CancellationToken cancellationToken)
    {
        Verifications.Add((email, token));
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string email, string token, CancellationToken cancellationToken)
    {
        PasswordResets.Add((email, token));
        return Task.CompletedTask;
    }
}

internal sealed class CountingUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}
