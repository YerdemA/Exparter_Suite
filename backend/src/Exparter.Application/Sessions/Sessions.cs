using Exparter.Application.Common;
using Exparter.Application.Matching;
using Exparter.Application.Notifications;
using Exparter.Domain.Notifications;
using Exparter.Domain.Sessions;
using FluentValidation;
using Mediator;

namespace Exparter.Application.Sessions;

/// <summary>
/// Methods that take <c>now</c> treat a proposal past its start time as declined and a
/// completion mark past its deadline as completed, whether or not that is recorded yet.
/// </summary>
public interface ISessionRepository
{
    Task<SwapSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Sessions a user cancelled, with the reason they gave; newest first.</summary>
    Task<PagedList<SwapSession>> ListCancelledByUsersAsync(PageRequest page, CancellationToken cancellationToken);

    /// <summary>Newest first, by scheduled time.</summary>
    Task<PagedList<SwapSession>> ListByMatchAsync(Guid matchId, PageRequest page, CancellationToken cancellationToken);

    /// <inheritdoc cref="ListByMatchAsync"/>
    Task<PagedList<SwapSession>> ListForUserAsync(
        Guid userId,
        SwapSessionStatus? status,
        DateTimeOffset? from,
        DateTimeOffset? to,
        DateTimeOffset now,
        PageRequest page,
        CancellationToken cancellationToken);

    /// <summary>The proposed and scheduled sessions of a match.</summary>
    Task<IReadOnlyList<SwapSession>> ListUpcomingByMatchAsync(Guid matchId, CancellationToken cancellationToken);

    /// <summary>
    /// The online sessions of any of the users that may overlap the given time, whatever
    /// their status; callers check the exact overlap and whether a session still counts.
    /// </summary>
    Task<IReadOnlyList<SwapSession>> ListAroundAsync(
        IReadOnlyCollection<Guid> userIds,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken);

    /// <summary>Sessions whose stored status is behind a passed deadline, oldest first.</summary>
    Task<IReadOnlyList<SwapSession>> ListOverdueAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// Sessions that may be due a reminder: scheduled ones starting within a day, and
    /// completion marks in their last day.
    /// </summary>
    Task<IReadOnlyList<SwapSession>> ListDueForReminderAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken);

    Task<int> CountCompletedForUserAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Those of the matches that have at least one completed session.</summary>
    Task<IReadOnlySet<Guid>> ListMatchIdsWithCompletedSessionAsync(
        IReadOnlyCollection<Guid> matchIds,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>The earliest completed session of the two users, in any of their matches.</summary>
    Task<Guid?> FindCompletedBetweenAsync(Guid userId, Guid otherUserId, DateTimeOffset now, CancellationToken cancellationToken);

    void Add(SwapSession session);
}

/// <summary>What other modules may know about sessions.</summary>
public interface ISessionDirectory
{
    Task<int> CountCompletedAsync(Guid userId, CancellationToken cancellationToken);

    /// <inheritdoc cref="ISessionRepository.ListMatchIdsWithCompletedSessionAsync"/>
    Task<IReadOnlySet<Guid>> GetMatchIdsWithCompletedSessionAsync(
        IReadOnlyCollection<Guid> matchIds,
        CancellationToken cancellationToken);

    /// <returns>The id of a completed session of the two users, or null if they have none.</returns>
    Task<Guid?> FindCompletedBetweenAsync(Guid userId, Guid otherUserId, CancellationToken cancellationToken);
}

internal sealed class SessionDirectory(ISessionRepository sessions, TimeProvider timeProvider) : ISessionDirectory
{
    public Task<int> CountCompletedAsync(Guid userId, CancellationToken cancellationToken) =>
        sessions.CountCompletedForUserAsync(userId, timeProvider.GetUtcNow(), cancellationToken);

    public Task<IReadOnlySet<Guid>> GetMatchIdsWithCompletedSessionAsync(
        IReadOnlyCollection<Guid> matchIds,
        CancellationToken cancellationToken) =>
        sessions.ListMatchIdsWithCompletedSessionAsync(matchIds, timeProvider.GetUtcNow(), cancellationToken);

    public Task<Guid?> FindCompletedBetweenAsync(Guid userId, Guid otherUserId, CancellationToken cancellationToken) =>
        sessions.FindCompletedBetweenAsync(userId, otherUserId, timeProvider.GetUtcNow(), cancellationToken);
}

public sealed record SwapSessionDto(
    Guid Id,
    Guid MatchId,
    Guid ProposedByUserId,
    DateTimeOffset ScheduledAt,
    int DurationMinutes,
    string? Note,
    SwapSessionStatus Status,
    Guid? CompletionMarkedByUserId,
    DateTimeOffset? CompletionMarkedAt,
    DateTimeOffset? CompletionConfirmDeadline,
    DateTimeOffset? CompletedAt,
    bool CompletedAutomatically,
    Guid? CancelledByUserId,
    string? CancelReason,
    DateTimeOffset? CancelledAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset CallOpensAt,
    DateTimeOffset CallClosesAt)
{
    // App versions up to 1.0.0+8 cannot read a session without the mode they used to
    // tell two kinds of session apart by. Remove once those versions are no longer in use.
    public string Mode => "Online";

    /// <summary>Describes the session as it stands at <paramref name="now"/>, passed deadlines included.</summary>
    public static SwapSessionDto From(SwapSession session, DateTimeOffset now)
    {
        var status = session.StatusAt(now);
        var completedByDeadline = status == SwapSessionStatus.Completed && session.Status == SwapSessionStatus.CompletionPending;
        return new SwapSessionDto(
            session.Id,
            session.MatchId,
            session.ProposedByUserId,
            session.ScheduledAt,
            session.DurationMinutes,
            session.Note,
            status,
            session.CompletionMarkedByUserId,
            session.CompletionMarkedAt,
            completedByDeadline ? null : session.CompletionConfirmDeadline,
            completedByDeadline ? session.CompletionConfirmDeadline : session.CompletedAt,
            completedByDeadline || session.CompletedAutomatically,
            session.CancelledByUserId,
            session.CancelReason,
            session.CancelledAt,
            session.CreatedAt,
            session.CallOpensAt,
            session.CallClosesAt);
    }
}

public static class SessionConflicts
{
    public static AppException TimeTaken() =>
        AppException.Conflict("session_time_taken", "The user already has a session at this time.");

    public static AppException OtherUserBusy() =>
        AppException.Conflict("other_user_busy", "The other user already has a session at this time.");
}

internal static class SessionExtensions
{
    /// <summary>
    /// A session is visible only to the two users of its match; to anyone else it does
    /// not exist, so that ids cannot be probed.
    /// </summary>
    public static async Task<SwapSession> GetForParticipantAsync(
        this ISessionRepository sessions,
        Guid sessionId,
        Guid userId,
        CancellationToken cancellationToken) =>
        await sessions.GetByIdAsync(sessionId, cancellationToken) is { } session && session.IsParticipant(userId)
            ? session
            : throw AppException.NotFound("The session was not found.");

    /// <summary>
    /// What a notification about the session carries. Its deadline is whichever moment
    /// the session is waiting for: the answer to a completion mark, or else its start.
    /// </summary>
    public static NotificationData NotificationData(this SwapSession session, Guid? actorUserId) =>
        new(
            ActorUserId: actorUserId,
            MatchId: session.MatchId,
            SessionId: session.Id,
            Reason: session.CancelReason,
            Deadline: session.CompletionConfirmDeadline ?? session.ScheduledAt);

    /// <summary>The match, if the user is part of it; otherwise the same answer as for an unknown id.</summary>
    public static async Task<MatchInfo> GetForParticipantAsync(
        this IMatchDirectory matches,
        Guid matchId,
        Guid userId,
        CancellationToken cancellationToken) =>
        await matches.FindAsync(matchId, cancellationToken) is { } match && match.IsParticipant(userId)
            ? match
            : throw AppException.NotFound("The match was not found.");

    /// <summary>
    /// Nobody can be in two sessions at once, whichever matches they belong to. Checked
    /// before a session is proposed and again when it is accepted, since either user may
    /// have planned something else at that time meanwhile.
    /// </summary>
    public static async Task EnsureTimeIsFreeAsync(
        this ISessionRepository sessions,
        SwapSession session,
        Guid actingUserId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var start = session.ScheduledAt;
        var end = session.EndsAt;
        var otherUserId = session.OtherThan(actingUserId);
        var overlapping = (await sessions.ListAroundAsync([actingUserId, otherUserId], start, end, cancellationToken))
            .Where(other => other.Id != session.Id && other.OverlapsWith(start, end))
            .ToList();

        if (overlapping.Any(other => other.OccupiesTimeOf(actingUserId, now)))
        {
            throw SessionConflicts.TimeTaken();
        }

        if (overlapping.Any(other => other.OccupiesTimeOf(otherUserId, now)))
        {
            throw SessionConflicts.OtherUserBusy();
        }
    }

    /// <summary>New sessions belong to a match that is running and not about to end.</summary>
    public static void EnsureAcceptsSessions(this MatchInfo match)
    {
        if (match.HasPendingEndRequest)
        {
            throw AppException.Conflict("match_end_pending", "No session can be added while a request to end the match is pending.");
        }

        if (!match.IsActive)
        {
            throw AppException.Conflict("invalid_state", "Sessions can be added only to an active match.");
        }
    }
}

public sealed record ProposeSessionCommand(Guid UserId, Guid MatchId, DateTimeOffset ScheduledAt, int DurationMinutes, string? Note)
    : ICommand<SwapSessionDto>;

public sealed class ProposeSessionCommandValidator : AbstractValidator<ProposeSessionCommand>
{
    public ProposeSessionCommandValidator()
    {
        RuleFor(x => x.DurationMinutes)
            .Must(SwapSession.AllowedDurations.Contains)
            .WithMessage($"The duration must be one of {string.Join(", ", SwapSession.AllowedDurations)} minutes.");
        RuleFor(x => x.Note).MaximumLength(SwapSession.NoteMaxLength);
    }
}

public sealed class ProposeSessionCommandHandler(
    IMatchDirectory matches,
    ISessionRepository sessions,
    IUserNotifier notifier,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<ProposeSessionCommand, SwapSessionDto>
{
    public async ValueTask<SwapSessionDto> Handle(ProposeSessionCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var match = await matches.GetForParticipantAsync(command.MatchId, command.UserId, cancellationToken);
        match.EnsureAcceptsSessions();

        var session = SwapSession.Propose(
            match.Id,
            command.UserId,
            match.OtherThan(command.UserId),
            command.ScheduledAt,
            command.DurationMinutes,
            command.Note,
            now);
        await sessions.EnsureTimeIsFreeAsync(session, command.UserId, now, cancellationToken);
        sessions.Add(session);
        notifier.Notify(session.OtherUserId, NotificationType.SessionProposed, session.NotificationData(command.UserId));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return SwapSessionDto.From(session, now);
    }
}

public sealed record ListMatchSessionsQuery(Guid UserId, Guid MatchId, int? Page, int? PageSize) : IQuery<PagedList<SwapSessionDto>>;

public sealed class ListMatchSessionsQueryHandler(IMatchDirectory matches, ISessionRepository sessions, TimeProvider timeProvider)
    : IQueryHandler<ListMatchSessionsQuery, PagedList<SwapSessionDto>>
{
    public async ValueTask<PagedList<SwapSessionDto>> Handle(ListMatchSessionsQuery query, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        await matches.GetForParticipantAsync(query.MatchId, query.UserId, cancellationToken);
        return (await sessions.ListByMatchAsync(query.MatchId, PageRequest.From(query.Page, query.PageSize), cancellationToken))
            .Map(session => SwapSessionDto.From(session, now));
    }
}

public sealed record ListSessionsQuery(
    Guid UserId,
    SwapSessionStatus? Status,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int? Page,
    int? PageSize) : IQuery<PagedList<SwapSessionDto>>;

public sealed class ListSessionsQueryValidator : AbstractValidator<ListSessionsQuery>
{
    public ListSessionsQueryValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
    }
}

public sealed class ListSessionsQueryHandler(ISessionRepository sessions, TimeProvider timeProvider)
    : IQueryHandler<ListSessionsQuery, PagedList<SwapSessionDto>>
{
    public async ValueTask<PagedList<SwapSessionDto>> Handle(ListSessionsQuery query, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        return (await sessions.ListForUserAsync(
                query.UserId,
                query.Status,
                query.From,
                query.To,
                now,
                PageRequest.From(query.Page, query.PageSize),
                cancellationToken))
            .Map(session => SwapSessionDto.From(session, now));
    }
}

public sealed record GetSessionQuery(Guid UserId, Guid SessionId) : IQuery<SwapSessionDto>;

public sealed class GetSessionQueryHandler(ISessionRepository sessions, TimeProvider timeProvider)
    : IQueryHandler<GetSessionQuery, SwapSessionDto>
{
    public async ValueTask<SwapSessionDto> Handle(GetSessionQuery query, CancellationToken cancellationToken) =>
        SwapSessionDto.From(
            await sessions.GetForParticipantAsync(query.SessionId, query.UserId, cancellationToken),
            timeProvider.GetUtcNow());
}

public enum SessionAction
{
    Accept,
    Decline,
    MarkCompleted,
    ConfirmCompleted,
}

public sealed record ChangeSessionCommand(Guid UserId, Guid SessionId, SessionAction Action) : ICommand<SwapSessionDto>;

public sealed class ChangeSessionCommandHandler(
    ISessionRepository sessions,
    IUserNotifier notifier,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<ChangeSessionCommand, SwapSessionDto>
{
    public async ValueTask<SwapSessionDto> Handle(ChangeSessionCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var session = await sessions.GetForParticipantAsync(command.SessionId, command.UserId, cancellationToken);
        NotificationType type;
        switch (command.Action)
        {
            case SessionAction.Accept:
                await sessions.EnsureTimeIsFreeAsync(session, command.UserId, now, cancellationToken);
                session.Accept(command.UserId, now);
                type = NotificationType.SessionAccepted;
                break;
            case SessionAction.Decline:
                session.Decline(command.UserId, now);
                type = NotificationType.SessionDeclined;
                break;
            case SessionAction.MarkCompleted:
                session.MarkCompleted(command.UserId, now);
                type = NotificationType.SessionCompletionMarked;
                break;
            default:
                session.ConfirmCompleted(command.UserId, now);
                type = NotificationType.SessionCompleted;
                break;
        }

        notifier.Notify(session.OtherThan(command.UserId), type, session.NotificationData(command.UserId));

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return SwapSessionDto.From(session, now);
    }
}

public sealed record CancelSessionCommand(Guid UserId, Guid SessionId, string Reason) : ICommand<SwapSessionDto>;

public sealed class CancelSessionCommandValidator : AbstractValidator<CancelSessionCommand>
{
    public CancelSessionCommandValidator()
    {
        RuleFor(x => x.Reason)
            .Must(reason => reason?.Trim().Length is >= SwapSession.CancelReasonMinLength and <= SwapSession.CancelReasonMaxLength)
            .WithMessage($"The reason must be {SwapSession.CancelReasonMinLength} to {SwapSession.CancelReasonMaxLength} characters.");
    }
}

public sealed class CancelSessionCommandHandler(
    ISessionRepository sessions,
    IUserNotifier notifier,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<CancelSessionCommand, SwapSessionDto>
{
    public async ValueTask<SwapSessionDto> Handle(CancelSessionCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var session = await sessions.GetForParticipantAsync(command.SessionId, command.UserId, cancellationToken);
        session.Cancel(command.UserId, command.Reason, now);
        notifier.Notify(session.OtherThan(command.UserId), NotificationType.SessionCancelled, session.NotificationData(command.UserId));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return SwapSessionDto.From(session, now);
    }
}

/// <summary>Cancels the sessions still ahead of a match that ended, in the transaction that ends it.</summary>
public sealed class CancelSessionsOnMatchEnded(ISessionRepository sessions, TimeProvider timeProvider)
    : INotificationHandler<MatchEnded>
{
    public async ValueTask Handle(MatchEnded notification, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        foreach (var session in await sessions.ListUpcomingByMatchAsync(notification.MatchId, cancellationToken))
        {
            session.CancelBecauseMatchEnded(now);
        }
    }
}

/// <summary>
/// Records the session deadlines that have passed (unanswered proposals and unconfirmed
/// completions) and reminds of sessions about to start and of marks about to stand.
/// </summary>
public sealed record ProcessSessionDeadlinesCommand : ICommand<int>;

public sealed class ProcessSessionDeadlinesCommandHandler(
    ISessionRepository sessions,
    IUserNotifier notifier,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<ProcessSessionDeadlinesCommand, int>
{
    /// <summary>Per run; whatever is left over is picked up by the next run.</summary>
    private const int BatchSize = 200;

    /// <returns>How many sessions were updated, reminders included.</returns>
    public async ValueTask<int> Handle(ProcessSessionDeadlinesCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var overdue = await sessions.ListOverdueAsync(now, BatchSize, cancellationToken);
        foreach (var session in overdue)
        {
            session.RecordPassedDeadline(now);
            if (session.Status == SwapSessionStatus.Declined)
            {
                notifier.Notify(session.ProposedByUserId, NotificationType.SessionDeclined, session.NotificationData(null));
            }
            else
            {
                notifier.Notify(session.ProposedByUserId, NotificationType.SessionCompleted, session.NotificationData(null));
                notifier.Notify(session.OtherUserId, NotificationType.SessionCompleted, session.NotificationData(null));
            }
        }

        var reminded = 0;
        foreach (var session in await sessions.ListDueForReminderAsync(now, BatchSize, cancellationToken))
        {
            if (session.TakeDueStartReminder(now))
            {
                reminded++;
                notifier.Notify(session.ProposedByUserId, NotificationType.SessionStartingSoon, session.NotificationData(null));
                notifier.Notify(session.OtherUserId, NotificationType.SessionStartingSoon, session.NotificationData(null));
            }
            else if (session.TakeDueCompletionReminder(now))
            {
                reminded++;
                notifier.Notify(
                    session.OtherThan(session.CompletionMarkedByUserId!.Value),
                    NotificationType.SessionCompletionReminder,
                    session.NotificationData(session.CompletionMarkedByUserId));
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return overdue.Count + reminded;
    }
}

public sealed record SessionCancellationDto(
    Guid SessionId,
    Guid MatchId,
    Guid CancelledByUserId,
    Guid OtherUserId,
    string Reason,
    DateTimeOffset CancelledAt);

/// <summary>Cancellation reasons are seen by the two users and by the administrators.</summary>
public sealed record AdminListSessionCancellationsQuery(int? Page, int? PageSize) : IQuery<PagedList<SessionCancellationDto>>;

public sealed class AdminListSessionCancellationsQueryHandler(ISessionRepository sessions)
    : IQueryHandler<AdminListSessionCancellationsQuery, PagedList<SessionCancellationDto>>
{
    public async ValueTask<PagedList<SessionCancellationDto>> Handle(
        AdminListSessionCancellationsQuery query,
        CancellationToken cancellationToken) =>
        (await sessions.ListCancelledByUsersAsync(PageRequest.From(query.Page, query.PageSize), cancellationToken))
        .Map(session => new SessionCancellationDto(
            session.Id,
            session.MatchId,
            session.CancelledByUserId!.Value,
            session.OtherThan(session.CancelledByUserId.Value),
            session.CancelReason!,
            session.CancelledAt!.Value));
}
