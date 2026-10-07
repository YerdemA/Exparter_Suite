using Exparter.Application.Identity;
using Exparter.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Exparter.Infrastructure.Persistence.Identity;

internal sealed class UserRepository(AppDbContext dbContext) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Users.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        dbContext.Users.FirstOrDefaultAsync(x => x.Email == normalizedEmail, cancellationToken);

    public Task<User?> GetByUsernameAsync(string normalizedUsername, CancellationToken cancellationToken) =>
        dbContext.Users.FirstOrDefaultAsync(x => x.Username == normalizedUsername, cancellationToken);

    public void Add(User user) => dbContext.Users.Add(user);
}

internal sealed class RefreshTokenRepository(AppDbContext dbContext) : IRefreshTokenRepository
{
    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        dbContext.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

    public async Task<IReadOnlyList<RefreshToken>> ListByFamilyAsync(Guid familyId, CancellationToken cancellationToken) =>
        await dbContext.RefreshTokens.Where(x => x.FamilyId == familyId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RefreshToken>> ListActiveByUserAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        await dbContext.RefreshTokens
            .Where(x => x.UserId == userId && x.RevokedAt == null && x.ExpiresAt > now)
            .ToListAsync(cancellationToken);

    public void Add(RefreshToken token) => dbContext.RefreshTokens.Add(token);
}

internal sealed class OneTimeTokenRepository(AppDbContext dbContext) : IOneTimeTokenRepository
{
    public Task<OneTimeToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        dbContext.OneTimeTokens.FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

    public async Task<IReadOnlyList<OneTimeToken>> ListUnusedAsync(
        Guid userId,
        OneTimeTokenPurpose purpose,
        CancellationToken cancellationToken) =>
        await dbContext.OneTimeTokens
            .Where(x => x.UserId == userId && x.Purpose == purpose && x.UsedAt == null)
            .ToListAsync(cancellationToken);

    public void Add(OneTimeToken token) => dbContext.OneTimeTokens.Add(token);
}
