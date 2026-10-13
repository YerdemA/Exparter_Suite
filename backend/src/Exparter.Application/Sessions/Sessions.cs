using Exparter.Application.Common;
using Exparter.Application.Matching;
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

    /// <summary>Newest first, by scheduled time (or, for in-person meetings, when they were reported).</summary>
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

    /// <summary>Sessions whose stored status is behind a passed deadline, oldest first.</summary>
    Task<IReadOnlyList<SwapSession>> ListOverdueAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken);

    Task<int> CountCompletedForUserAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Those of the matches that have at least one completed session.</summary>
    Task<IReadOnlySet<Guid>> ListMatchIdsWithCompletedSessionAsync(
        IReadOnlyCollection<Guid> matchIds,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>
    /// Whether the two users have an in-person meeting on record, in any of their
    /// matches, that was not cancelled.
    /// </summary>
    Task<bool> HasInPersonMeetingBetweenAsync(Guid userId, Guid otherUserId, CancellationToken cancellationToken);

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
}

internal sealed class SessionDirectory(ISessionRepository sessions, TimeProvider timeProvider) : ISessionDirectory
{
    public Task<int> CountCompletedAsync(Guid userId, CancellationToken cancellationToken) =>
        sessions.CountCompletedForUserAsync(userId, timeProvider.GetUtcNow(), cancellationToken);

    public Task<IReadOnlySet<Guid>> GetMatchIdsWithCompletedSessionAsync(
        IReadOnlyCollection<Guid> matchIds,
        CancellationToken cancellationToken) =>
        sessions.ListMatchIdsWithCompletedSessionAsync(matchIds, timeProvider.GetUtcNow(), cancellationToken);
}

public sealed record SwapSessionDto(
    Guid Id,
    Guid MatchId,
    Guid ProposedByUserId,
    DateTimeOffset? ScheduledAt,
    int? DurationMinutes,
    SessionMode Mode,
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
    DateTimeOffset CreatedAt)
{
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
            session.Mode,
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
            session.CreatedAt);
    }
}

public static class SessionConflicts
{
    public static AppException InPersonAlreadyReported() =>
        AppException.Conflict("in_person_already_reported", "The two users have already reported an in-person meeting.");
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

    /// <summary>The match, if the user is part of it; otherwise the same answer as for an unknown id.</summary>
    public static async Task<MatchInfo> GetForParticipantAsync(
        this IMatchDirectory matches,
        Guid matchId,
        Guid userId,
        CancellationToken cancellationToken) =>
        await matches.FindAsync(matchId, cancellationToken) is { } match && match.IsParticipant(userId)
            ? match
            : throw AppException.NotFound("The match was not found.");

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
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<ProposeSessionCommand, SwapSessionDto>
{
    public async ValueTask<SwapSessionDto> Handle(ProposeSessionCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var match = await matches.GetForParticipantAsync(command.MatchId, command.UserId, cancellationToken);
        match.EnsureAcceptsSessions();

        var session = SwapSession.ProposeOnline(
            match.Id,
            command.UserId,
            match.OtherThan(command.UserId),
            command.ScheduledAt,
            command.DurationMinutes,
            command.Note,
            now);
        sessions.Add(session);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return SwapSessionDto.From(session, now);
    }
}

public sealed record ReportInPersonSessionCommand(Guid UserId, Guid MatchId) : ICommand<SwapSessionDto>;

public sealed class ReportInPersonSessionCommandHandler(
    IMatchDirectory matches,
    ISessionRepository sessions,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<ReportInPersonSessionCommand, SwapSessionDto>
{
    public async ValueTask<SwapSessionDto> Handle(ReportInPersonSessionCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var match = await matches.GetForParticipantAsync(command.MatchId, command.UserId, cancellationToken);
        match.EnsureAcceptsSessions();

        // Nobody else sees an in-person meeting take place, so two users could inflate
        // their statistics with reports; one per pair is all that counts.
        var otherUserId = match.OtherThan(command.UserId);
        if (await sessions.HasInPersonMeetingBetweenAsync(command.UserId, otherUserId, cancellationToken))
        {
            throw SessionConflicts.InPersonAlreadyReported();
        }

        var session = SwapSession.ReportInPerson(match.Id, command.UserId, otherUserId, now);
        sessions.Add(session);
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

public sealed class ChangeSessionCommandHandler(ISessionRepository sessions, IUnitOfWork unitOfWork, TimeProvider timeProvider)
    : ICommandHandler<ChangeSessionCommand, SwapSessionDto>
{
    public async ValueTask<SwapSessionDto> Handle(ChangeSessionCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var session = await sessions.GetForParticipantAsync(command.SessionId, command.UserId, cancellationToken);
        switch (command.Action)
        {
            case SessionAction.Accept:
                session.Accept(command.UserId, now);
                break;
            case SessionAction.Decline:
                session.Decline(command.UserId, now);
                break;
            case SessionAction.MarkCompleted:
                session.MarkCompleted(command.UserId, now);
                break;
            default:
                session.ConfirmCompleted(command.UserId, now);
                break;
        }

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

public sealed class CancelSessionCommandHandler(ISessionRepository sessions, IUnitOfWork unitOfWork, TimeProvider timeProvider)
    : ICommandHandler<CancelSessionCommand, SwapSessionDto>
{
    public async ValueTask<SwapSessionDto> Handle(CancelSessionCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var session = await sessions.GetForParticipantAsync(command.SessionId, command.UserId, cancellationToken);
        session.Cancel(command.UserId, command.Reason, now);
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

/// <summary>Records the session deadlines that have passed: unanswered proposals and unconfirmed completions.</summary>
public sealed record ProcessSessionDeadlinesCommand : ICommand<int>;

public sealed class ProcessSessionDeadlinesCommandHandler(
    ISessionRepository sessions,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<ProcessSessionDeadlinesCommand, int>
{
    /// <summary>Per run; whatever is left over is picked up by the next run.</summary>
    private const int BatchSize = 200;

    /// <returns>How many sessions were updated.</returns>
    public async ValueTask<int> Handle(ProcessSessionDeadlinesCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var overdue = await sessions.ListOverdueAsync(now, BatchSize, cancellationToken);
        foreach (var session in overdue)
        {
            session.RecordPassedDeadline(now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return overdue.Count;
    }
}
