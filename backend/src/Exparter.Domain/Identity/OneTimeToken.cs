namespace Exparter.Domain.Identity;

public sealed class OneTimeToken
{
    private OneTimeToken()
    {
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public OneTimeTokenPurpose Purpose { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? UsedAt { get; private set; }

    public static TimeSpan LifetimeOf(OneTimeTokenPurpose purpose) => purpose switch
    {
        OneTimeTokenPurpose.EmailVerification => TimeSpan.FromHours(24),
        OneTimeTokenPurpose.PasswordReset => TimeSpan.FromHours(1),
        _ => throw new ArgumentOutOfRangeException(nameof(purpose)),
    };

    public static OneTimeToken Issue(Guid userId, OneTimeTokenPurpose purpose, string tokenHash, DateTimeOffset now) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Purpose = purpose,
            TokenHash = tokenHash,
            ExpiresAt = now + LifetimeOf(purpose),
        };

    public bool CanBeUsed(OneTimeTokenPurpose expectedPurpose, DateTimeOffset now) =>
        Purpose == expectedPurpose && UsedAt is null && now < ExpiresAt;

    public void Use(OneTimeTokenPurpose expectedPurpose, DateTimeOffset now)
    {
        if (!CanBeUsed(expectedPurpose, now))
        {
            throw IdentityErrors.InvalidToken();
        }

        UsedAt = now;
    }

    /// <summary>Makes an unused token unusable, for example when a newer one replaces it.</summary>
    public void Invalidate(DateTimeOffset now) => UsedAt ??= now;
}
