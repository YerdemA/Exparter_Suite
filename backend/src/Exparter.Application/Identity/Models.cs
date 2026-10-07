using Exparter.Domain.Identity;

namespace Exparter.Application.Identity;

public sealed record AccountDto(
    Guid Id,
    string Email,
    string Username,
    bool EmailVerified,
    UserRole Role,
    DateTimeOffset CreatedAt)
{
    public static AccountDto From(User user) =>
        new(user.Id, user.Email, user.Username, user.IsEmailVerified, user.Role, user.CreatedAt);
}

public sealed record TokenPair(string AccessToken, string RefreshToken, int ExpiresIn);

public sealed record AuthResult(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    AccountDto Account,
    bool AccountRestored);

public sealed record AccountDeletionScheduled(DateTimeOffset PermanentDeletionAt);
