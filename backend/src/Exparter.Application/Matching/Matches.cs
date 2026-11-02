using Exparter.Application.Common;
using Exparter.Application.Identity;
using Exparter.Application.Moderation;
using Exparter.Application.Notifications;
using Exparter.Domain.Matching;
using Exparter.Domain.Notifications;
using FluentValidation;
using Mediator;

namespace Exparter.Application.Matching;

public sealed record ListMatchesQuery(Guid UserId, MatchStatus? Status, Guid? OtherUserId, int? Page, int? PageSize) : IQuery<PagedList<MatchDto>>;

public sealed class ListMatchesQueryValidator : AbstractValidator<ListMatchesQuery>
{
    public ListMatchesQueryValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
    }
}

public sealed class ListMatchesQueryHandler(IMatchRepository matches, MatchingDtoFactory dtos)
    : IQueryHandler<ListMatchesQuery, PagedList<MatchDto>>
{
    public async ValueTask<PagedList<MatchDto>> Handle(ListMatchesQuery query, CancellationToken cancellationToken)
    {
        var page = await matches.ListAsync(query.UserId, query.Status, query.OtherUserId, PageRequest.From(query.Page, query.PageSize), cancellationToken);
        return new PagedList<MatchDto>(
            await dtos.ToDtosAsync(page.Items, query.UserId, cancellationToken),
            page.Page,
            page.PageSize,
            page.TotalCount);
    }
}

public sealed record GetMatchQuery(Guid UserId, Guid MatchId) : IQuery<MatchDto>;

public sealed class GetMatchQueryHandler(IMatchRepository matches, MatchingDtoFactory dtos) : IQueryHandler<GetMatchQuery, MatchDto>
{
    public async ValueTask<MatchDto> Handle(GetMatchQuery query, CancellationToken cancellationToken) =>
        await dtos.ToDtoAsync(
            await matches.GetForParticipantAsync(query.MatchId, query.UserId, cancellationToken),
            query.UserId,
            cancellationToken);
}

public enum MatchEndAction
{
    /// <summary>Ask the other user to end the match.</summary>
    Request,

    /// <summary>Take back one's own end request.</summary>
    Withdraw,

    /// <summary>Agree to the other user's end request.</summary>
    Accept,

    /// <summary>Turn down the other user's end request.</summary>
    Reject,
}

public sealed record ChangeMatchEndCommand(Guid UserId, Guid MatchId, MatchEndAction Action) : ICommand<MatchDto>;

public sealed class ChangeMatchEndCommandHandler(
    IMatchRepository matches,
    MatchingDtoFactory dtos,
    IPublisher publisher,
    IUserNotifier notifier,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<ChangeMatchEndCommand, MatchDto>
{
    public async ValueTask<MatchDto> Handle(ChangeMatchEndCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var match = await matches.GetForParticipantAsync(command.MatchId, command.UserId, cancellationToken);

        // An end request nobody answered in time has already taken effect, even if the
        // background job has not recorded it yet.
        match.EndIfEndRequestOverdue(now);
        switch (command.Action)
        {
            case MatchEndAction.Request:
                match.RequestEnd(command.UserId, now);
                break;
            case MatchEndAction.Withdraw:
                match.WithdrawEndRequest(command.UserId);
                break;
            case MatchEndAction.Accept:
                match.AcceptEnd(command.UserId, now);
                break;
            default:
                match.RejectEnd(command.UserId);
                break;
        }

        if (match.Status == MatchStatus.Ended)
        {
            await publisher.Publish(new MatchEnded(match.Id), cancellationToken);
        }

        notifier.Notify(
            match.OtherThan(command.UserId),
            command.Action switch
            {
                MatchEndAction.Request => NotificationType.MatchEndRequested,
                MatchEndAction.Withdraw => NotificationType.MatchEndRequestWithdrawn,
                MatchEndAction.Accept => NotificationType.MatchEnded,
                _ => NotificationType.MatchEndRequestRejected,
            },
            new NotificationData(ActorUserId: command.UserId, MatchId: match.Id, Deadline: match.EndRequestExpiresAt));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await dtos.ToDtoAsync(match, command.UserId, cancellationToken);
    }
}

/// <summary>
/// Records the deadlines that have passed (unanswered swap requests and end requests)
/// and reminds of the end requests about to take effect.
/// </summary>
public sealed record ProcessMatchingDeadlinesCommand : ICommand<int>;

public sealed class ProcessMatchingDeadlinesCommandHandler(
    ISwapRequestRepository requests,
    IMatchRepository matches,
    IPublisher publisher,
    IUserNotifier notifier,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<ProcessMatchingDeadlinesCommand, int>
{
    /// <summary>Per run; whatever is left over is picked up by the next run.</summary>
    private const int BatchSize = 200;

    /// <returns>How many requests and matches were updated, reminders included.</returns>
    public async ValueTask<int> Handle(ProcessMatchingDeadlinesCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var overdueRequests = await requests.ListOverdueAsync(now, BatchSize, cancellationToken);
        foreach (var request in overdueRequests)
        {
            request.ExpireIfOverdue(now);
            notifier.Notify(request.RequesterId, NotificationType.SwapRequestExpired, new NotificationData(SwapRequestId: request.Id));
        }

        var overdueMatches = await matches.ListWithOverdueEndRequestAsync(now, BatchSize, cancellationToken);
        foreach (var match in overdueMatches)
        {
            match.EndIfEndRequestOverdue(now);
            await publisher.Publish(new MatchEnded(match.Id), cancellationToken);
            foreach (var userId in new[] { match.UserAId, match.UserBId })
            {
                notifier.Notify(userId, NotificationType.MatchEnded, new NotificationData(MatchId: match.Id));
            }
        }

        var reminded = 0;
        foreach (var match in await matches.ListDueForEndReminderAsync(now, BatchSize, cancellationToken))
        {
            if (match.TakeDueEndReminder(now))
            {
                reminded++;
                notifier.Notify(
                    match.OtherThan(match.EndRequestedByUserId!.Value),
                    NotificationType.MatchEndReminder,
                    new NotificationData(
                        ActorUserId: match.EndRequestedByUserId,
                        MatchId: match.Id,
                        Deadline: match.EndRequestExpiresAt));
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return overdueRequests.Count + overdueMatches.Count + reminded;
    }
}

/// <summary>
/// A user is leaving: their pending requests are withdrawn and their matches end, in
/// the transaction that records the deletion request.
/// </summary>
public sealed class CloseMatchingOnAccountDeletion(
    ISwapRequestRepository requests,
    IMatchRepository matches,
    IPublisher publisher,
    IUserNotifier notifier,
    TimeProvider timeProvider) : INotificationHandler<AccountDeletionRequested>
{
    public async ValueTask Handle(AccountDeletionRequested notification, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var userId = notification.UserId;
        foreach (var request in await requests.ListPendingOfUserAsync(userId, cancellationToken))
        {
            if (request.StatusAt(now) != SwapRequestStatus.Pending)
            {
                continue;
            }

            request.CancelBecauseAccountDeleted(now);

            // The other user is told what it looked like from their side: a request they
            // received was withdrawn, or one they sent was never answered.
            if (request.RequesterId == userId)
            {
                notifier.Notify(
                    request.RecipientId,
                    NotificationType.SwapRequestCancelled,
                    new NotificationData(ActorUserId: userId, SwapRequestId: request.Id));
            }
            else
            {
                notifier.Notify(request.RequesterId, NotificationType.SwapRequestExpired, new NotificationData(SwapRequestId: request.Id));
            }
        }

        foreach (var match in await matches.ListOpenOfUserAsync(userId, cancellationToken))
        {
            match.EndBecauseAccountDeleted(now);
            await publisher.Publish(new MatchEnded(match.Id), cancellationToken);
            notifier.Notify(
                match.OtherThan(userId),
                NotificationType.MatchEnded,
                new NotificationData(ActorUserId: userId, MatchId: match.Id));
        }
    }
}

/// <summary>
/// A block withdraws the requests waiting between the two users and ends their match,
/// in the block's own transaction. Nobody is notified: the blocked user is not to learn
/// of the block, and the blocker knows.
/// </summary>
public sealed class CloseMatchingOnBlock(
    ISwapRequestRepository requests,
    IMatchRepository matches,
    IPublisher publisher,
    TimeProvider timeProvider) : INotificationHandler<UserBlocked>
{
    public async ValueTask Handle(UserBlocked notification, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var pending = await requests.FindPendingBetweenAsync(notification.BlockerId, notification.BlockedId, cancellationToken);
        pending?.CancelBecauseBlocked(now);

        if (await matches.FindOpenBetweenAsync(notification.BlockerId, notification.BlockedId, cancellationToken) is { } match)
        {
            match.EndBecauseBlocked(notification.BlockerId, now);
            await publisher.Publish(new MatchEnded(match.Id), cancellationToken);
        }
    }
}
