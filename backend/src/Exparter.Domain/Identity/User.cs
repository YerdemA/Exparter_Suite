namespace Exparter.Domain.Identity;

public sealed class User
{
    public static readonly TimeSpan DeletionGracePeriod = TimeSpan.FromDays(30);

    private User()
    {
    }

    public Guid Id { get; private set; }
    public string Email { get; private set; } = null!;
    public string Username { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public UserStatus Status { get; private set; }
    public UserRole Role { get; private set; }
    public DateTimeOffset? EmailVerifiedAt { get; private set; }
    public DateTimeOffset AgeConfirmedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? DeletionRequestedAt { get; private set; }

    public bool IsEmailVerified => EmailVerifiedAt is not null;

    public DateTimeOffset? PermanentDeletionAt => DeletionRequestedAt + DeletionGracePeriod;

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public static User Register(string email, string username, string passwordHash, bool ageConfirmed, DateTimeOffset now)
    {
        if (!ageConfirmed)
        {
            throw IdentityErrors.AgeNotConfirmed();
        }

        return new User
        {
            Id = Guid.CreateVersion7(),
            Email = NormalizeEmail(email),
            Username = Identity.Username.Parse(username),
            PasswordHash = passwordHash,
            Status = UserStatus.Active,
            Role = UserRole.User,
            AgeConfirmedAt = now,
            CreatedAt = now,
        };
    }

    public void VerifyEmail(DateTimeOffset now)
    {
        if (IsEmailVerified)
        {
            throw IdentityErrors.EmailAlreadyVerified();
        }

        EmailVerifiedAt = now;
    }

    public void ChangePassword(string passwordHash) => PasswordHash = passwordHash;

    public void ChangeUsername(string username) => Username = Identity.Username.Parse(username);

    public void RequestDeletion(DateTimeOffset now)
    {
        if (Status != UserStatus.Active)
        {
            throw IdentityErrors.InvalidState("Only an active account can be scheduled for deletion.");
        }

        Status = UserStatus.PendingDeletion;
        DeletionRequestedAt = now;
    }

    /// <summary>Restores an account whose deletion is still pending.</summary>
    /// <returns>True if a pending deletion was cancelled.</returns>
    public bool CancelDeletion()
    {
        if (Status != UserStatus.PendingDeletion)
        {
            return false;
        }

        Status = UserStatus.Active;
        DeletionRequestedAt = null;
        return true;
    }
}
