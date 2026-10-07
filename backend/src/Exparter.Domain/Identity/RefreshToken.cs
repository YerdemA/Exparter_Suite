namespace Exparter.Domain.Identity;

public sealed class RefreshToken
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    private RefreshToken()
    {
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = null!;

    /// <summary>Shared by every token descended from the same sign-in, so a stolen chain can be revoked as a whole.</summary>
    public Guid FamilyId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }

    public bool IsRevoked => RevokedAt is not null;

    public static RefreshToken IssueForSignIn(Guid userId, string tokenHash, DateTimeOffset now) =>
        Issue(userId, tokenHash, Guid.CreateVersion7(), now);

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    public bool IsActive(DateTimeOffset now) => !IsRevoked && !IsExpired(now);

    /// <summary>Revokes this token and issues its successor in the same family.</summary>
    public RefreshToken Rotate(string newTokenHash, DateTimeOffset now)
    {
        if (!IsActive(now))
        {
            throw IdentityErrors.InvalidState("Only an active refresh token can be rotated.");
        }

        var successor = Issue(UserId, newTokenHash, FamilyId, now);
        RevokedAt = now;
        ReplacedByTokenId = successor.Id;
        return successor;
    }

    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;

    private static RefreshToken Issue(Guid userId, string tokenHash, Guid familyId, DateTimeOffset now) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            TokenHash = tokenHash,
            FamilyId = familyId,
            CreatedAt = now,
            ExpiresAt = now + Lifetime,
        };
}
