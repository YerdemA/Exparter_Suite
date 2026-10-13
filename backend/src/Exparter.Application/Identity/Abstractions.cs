using Exparter.Domain.Identity;

namespace Exparter.Application.Identity;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<User>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<User?> GetByUsernameAsync(string normalizedUsername, CancellationToken cancellationToken);

    /// <summary>Accounts whose deletion can no longer be taken back, oldest request first.</summary>
    Task<IReadOnlyList<User>> ListDueForErasureAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken);

    void Add(User user);
}

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task<IReadOnlyList<RefreshToken>> ListByFamilyAsync(Guid familyId, CancellationToken cancellationToken);

    Task<IReadOnlyList<RefreshToken>> ListActiveByUserAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken);

    Task DeleteByUserAsync(Guid userId, CancellationToken cancellationToken);

    void Add(RefreshToken token);
}

public interface IOneTimeTokenRepository
{
    Task<OneTimeToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task<IReadOnlyList<OneTimeToken>> ListUnusedAsync(Guid userId, OneTimeTokenPurpose purpose, CancellationToken cancellationToken);

    Task DeleteByUserAsync(Guid userId, CancellationToken cancellationToken);

    void Add(OneTimeToken token);
}

public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string passwordHash, string password);
}

public sealed record AccessToken(string Value, int ExpiresInSeconds);

/// <summary>A random secret handed to the client; only <see cref="Hash"/> is stored.</summary>
public sealed record OpaqueToken(string Value, string Hash);

public interface ITokenService
{
    AccessToken CreateAccessToken(User user);

    OpaqueToken GenerateOpaqueToken();

    string HashOpaqueToken(string value);
}

/// <summary>Delivery is best effort: implementations log failures instead of throwing.</summary>
public interface IEmailSender
{
    Task SendEmailVerificationAsync(string email, string token, CancellationToken cancellationToken);

    Task SendPasswordResetAsync(string email, string token, CancellationToken cancellationToken);
}
