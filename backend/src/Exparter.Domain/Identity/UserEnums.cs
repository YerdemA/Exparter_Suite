namespace Exparter.Domain.Identity;

public enum UserStatus
{
    Active,
    Suspended,
    PendingDeletion,

    /// <summary>Erased: only the row is left, so that what the user wrote still points somewhere.</summary>
    Deleted,
}

public enum UserRole
{
    User,
    Admin,
}

public enum OneTimeTokenPurpose
{
    EmailVerification,
    PasswordReset,
}
