using Exparter.Application.Common;
using Exparter.Application.Sessions;
using Exparter.Domain.Sessions;
using Exparter.Infrastructure.Persistence.Skills;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Exparter.Infrastructure.Persistence.Sessions;

internal sealed class SwapSessionConfiguration : IEntityTypeConfiguration<SwapSession>
{
    public const string Schema = "sessions";

    public void Configure(EntityTypeBuilder<SwapSession> builder)
    {
        builder.ToTable("swap_sessions", Schema);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Mode).HasConversion<string>().HasMaxLength(16);
        builder.Property(x => x.Note).HasMaxLength(SwapSession.NoteMaxLength);
        builder.Property(x => x.CancelReason).HasMaxLength(SwapSession.CancelReasonMaxLength);

        // Two answers to the same session race on the status: the second one finds it
        // changed and is turned into a conflict instead of overwriting the first.
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsConcurrencyToken();

        // The match and the users are plain values: tables of different modules are not
        // linked by foreign keys (see ADR 0006).
        builder.HasIndex(x => new { x.MatchId, x.CreatedAt });
        builder.HasIndex(x => x.ProposedByUserId);
        builder.HasIndex(x => x.OtherUserId);
        builder.HasIndex(x => new { x.Status, x.ScheduledAt });
        builder.HasIndex(x => new { x.Status, x.CompletionConfirmDeadline });
    }
}

internal sealed class SessionRepository(AppDbContext dbContext) : ISessionRepository
{
    public Task<SwapSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.SwapSessions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<PagedList<SwapSession>> ListByMatchAsync(Guid matchId, PageRequest page, CancellationToken cancellationToken) =>
        Newest(dbContext.SwapSessions.AsNoTracking().Where(x => x.MatchId == matchId)).ToPageAsync(page, cancellationToken);

    public Task<PagedList<SwapSession>> ListForUserAsync(
        Guid userId,
        SwapSessionStatus? status,
        DateTimeOffset? from,
        DateTimeOffset? to,
        DateTimeOffset now,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var sessions = dbContext.SwapSessions.AsNoTracking().Where(x => x.ProposedByUserId == userId || x.OtherUserId == userId);
        sessions = status switch
        {
            null => sessions,
            SwapSessionStatus.Proposed => sessions.Where(x => x.Status == SwapSessionStatus.Proposed && x.ScheduledAt > now),
            SwapSessionStatus.Declined => sessions.Where(
                x => x.Status == SwapSessionStatus.Declined || (x.Status == SwapSessionStatus.Proposed && x.ScheduledAt <= now)),
            SwapSessionStatus.CompletionPending => sessions.Where(
                x => x.Status == SwapSessionStatus.CompletionPending && x.CompletionConfirmDeadline > now),
            SwapSessionStatus.Completed => Completed(sessions, now),
            _ => sessions.Where(x => x.Status == status),
        };

        if (from is not null)
        {
            sessions = sessions.Where(x => x.ScheduledAt >= from);
        }

        if (to is not null)
        {
            sessions = sessions.Where(x => x.ScheduledAt < to);
        }

        return Newest(sessions).ToPageAsync(page, cancellationToken);
    }

    public async Task<IReadOnlyList<SwapSession>> ListUpcomingByMatchAsync(Guid matchId, CancellationToken cancellationToken) =>
        await dbContext.SwapSessions
            .Where(x => x.MatchId == matchId
                && (x.Status == SwapSessionStatus.Proposed || x.Status == SwapSessionStatus.Scheduled))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SwapSession>> ListOverdueAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken) =>
        await dbContext.SwapSessions
            .Where(x => (x.Status == SwapSessionStatus.Proposed && x.ScheduledAt <= now)
                || (x.Status == SwapSessionStatus.CompletionPending && x.CompletionConfirmDeadline <= now))
            .OrderBy(x => x.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public Task<int> CountCompletedForUserAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        Completed(dbContext.SwapSessions.Where(x => x.ProposedByUserId == userId || x.OtherUserId == userId), now)
            .CountAsync(cancellationToken);

    public async Task<IReadOnlySet<Guid>> ListMatchIdsWithCompletedSessionAsync(
        IReadOnlyCollection<Guid> matchIds,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        (await Completed(dbContext.SwapSessions.Where(x => matchIds.Contains(x.MatchId)), now)
            .Select(x => x.MatchId)
            .Distinct()
            .ToListAsync(cancellationToken))
        .ToHashSet();

    public void Add(SwapSession session) => dbContext.SwapSessions.Add(session);

    private static IQueryable<SwapSession> Completed(IQueryable<SwapSession> sessions, DateTimeOffset now) =>
        sessions.Where(x => x.Status == SwapSessionStatus.Completed
            || (x.Status == SwapSessionStatus.CompletionPending && x.CompletionConfirmDeadline <= now));

    private static IQueryable<SwapSession> Newest(IQueryable<SwapSession> sessions) =>
        sessions.OrderByDescending(x => x.ScheduledAt ?? x.CreatedAt).ThenByDescending(x => x.Id);
}
