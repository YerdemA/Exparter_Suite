using Exparter.Application.Common;
using Exparter.Application.Matching;
using Exparter.Domain.Matching;
using Exparter.Infrastructure.Persistence.Skills;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Exparter.Infrastructure.Persistence.Matching;

internal static class MatchingSchema
{
    public const string Name = "matching";
}

internal sealed class SwapRequestConfiguration : IEntityTypeConfiguration<SwapRequest>
{
    public void Configure(EntityTypeBuilder<SwapRequest> builder)
    {
        builder.ToTable("swap_requests", MatchingSchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Message).HasMaxLength(SwapRequest.MessageMaxLength);

        // Two answers to the same request race on the status: the second one finds it
        // changed and is turned into a conflict instead of overwriting the first.
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(16).IsConcurrencyToken();

        builder.HasIndex(x => new { x.RecipientId, x.CreatedAt });
        builder.HasIndex(x => new { x.RequesterId, x.CreatedAt });
        builder.HasIndex(x => new { x.Status, x.ExpiresAt });
    }
}

internal sealed class MatchConfiguration : IEntityTypeConfiguration<Match>
{
    public void Configure(EntityTypeBuilder<Match> builder)
    {
        builder.ToTable("matches", MatchingSchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(16).IsConcurrencyToken();
        builder.Property(x => x.EndReason).HasConversion<string>().HasMaxLength(32);

        builder.HasIndex(x => x.SwapRequestId).IsUnique();
        builder.HasIndex(x => new { x.UserAId, x.CreatedAt });
        builder.HasIndex(x => new { x.UserBId, x.CreatedAt });
        builder.HasIndex(x => new { x.Status, x.EndRequestExpiresAt });
        builder.HasOne<SwapRequest>().WithMany().HasForeignKey(x => x.SwapRequestId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SwapRequestRepository(AppDbContext dbContext) : ISwapRequestRepository
{
    public Task<SwapRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.SwapRequests.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<SwapRequest?> FindPendingBetweenAsync(Guid firstUserId, Guid secondUserId, CancellationToken cancellationToken) =>
        dbContext.SwapRequests.FirstOrDefaultAsync(
            x => x.Status == SwapRequestStatus.Pending
                && ((x.RequesterId == firstUserId && x.RecipientId == secondUserId)
                    || (x.RequesterId == secondUserId && x.RecipientId == firstUserId)),
            cancellationToken);

    public Task<SwapRequest?> FindLastRefusedAsync(Guid requesterId, Guid recipientId, CancellationToken cancellationToken) =>
        dbContext.SwapRequests.AsNoTracking()
            .Where(x => x.RequesterId == requesterId
                && x.RecipientId == recipientId
                && (x.Status == SwapRequestStatus.Declined || x.Status == SwapRequestStatus.Expired))
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<PagedList<SwapRequest>> ListAsync(
        Guid userId,
        SwapRequestDirection direction,
        SwapRequestStatus? status,
        DateTimeOffset now,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var requests = dbContext.SwapRequests.AsNoTracking();
        requests = direction == SwapRequestDirection.Incoming
            ? requests.Where(x => x.RecipientId == userId)
            : requests.Where(x => x.RequesterId == userId);

        requests = status switch
        {
            null => requests,
            SwapRequestStatus.Pending => requests.Where(x => x.Status == SwapRequestStatus.Pending && x.ExpiresAt > now),
            SwapRequestStatus.Expired => requests.Where(
                x => x.Status == SwapRequestStatus.Expired || (x.Status == SwapRequestStatus.Pending && x.ExpiresAt <= now)),
            _ => requests.Where(x => x.Status == status),
        };

        return requests.OrderByDescending(x => x.CreatedAt).ToPageAsync(page, cancellationToken);
    }

    public async Task<IReadOnlyList<SwapRequest>> ListOverdueAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken) =>
        await dbContext.SwapRequests
            .Where(x => x.Status == SwapRequestStatus.Pending && x.ExpiresAt <= now)
            .OrderBy(x => x.ExpiresAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public Task<bool> UsesOfferedSkillAsync(Guid userId, Guid skillId, DateTimeOffset now, CancellationToken cancellationToken) =>
        dbContext.SwapRequests.AnyAsync(
            x => x.Status == SwapRequestStatus.Pending
                && x.ExpiresAt > now
                && ((x.RequesterId == userId && x.OfferedSkillId == skillId)
                    || (x.RecipientId == userId && x.RequestedSkillId == skillId)),
            cancellationToken);

    public void Add(SwapRequest request) => dbContext.SwapRequests.Add(request);
}

internal sealed class MatchRepository(AppDbContext dbContext) : IMatchRepository
{
    public Task<Match?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Matches.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> HasOpenBetweenAsync(Guid firstUserId, Guid secondUserId, CancellationToken cancellationToken) =>
        dbContext.Matches.AnyAsync(
            x => x.Status != MatchStatus.Ended
                && ((x.UserAId == firstUserId && x.UserBId == secondUserId)
                    || (x.UserAId == secondUserId && x.UserBId == firstUserId)),
            cancellationToken);

    public Task<PagedList<Match>> ListAsync(Guid userId, MatchStatus? status, PageRequest page, CancellationToken cancellationToken)
    {
        var matches = dbContext.Matches.AsNoTracking().Where(x => x.UserAId == userId || x.UserBId == userId);
        if (status is not null)
        {
            matches = matches.Where(x => x.Status == status);
        }

        return matches.OrderByDescending(x => x.CreatedAt).ToPageAsync(page, cancellationToken);
    }

    public async Task<IReadOnlyList<Match>> ListWithOverdueEndRequestAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken) =>
        await dbContext.Matches
            .Where(x => x.Status == MatchStatus.EndRequested && x.EndRequestExpiresAt <= now)
            .OrderBy(x => x.EndRequestExpiresAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public Task<bool> UsesOfferedSkillAsync(Guid userId, Guid skillId, CancellationToken cancellationToken) =>
        dbContext.Matches.AnyAsync(
            x => x.Status != MatchStatus.Ended
                && ((x.UserAId == userId && x.SkillTaughtByAId == skillId)
                    || (x.UserBId == userId && x.SkillTaughtByBId == skillId)),
            cancellationToken);

    public void Add(Match match) => dbContext.Matches.Add(match);
}
