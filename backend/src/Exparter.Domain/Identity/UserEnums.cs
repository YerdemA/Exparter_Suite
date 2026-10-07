namespace Exparter.Domain.Identity;

public enum UserStatus
{
    Active,
    Suspended,
    PendingDeletion,
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
