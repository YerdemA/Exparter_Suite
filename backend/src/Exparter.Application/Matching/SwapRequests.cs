using Exparter.Application.Common;
using Exparter.Application.Identity;
using Exparter.Application.Notifications;
using Exparter.Application.Skills;
using Exparter.Domain.Matching;
using Exparter.Domain.Notifications;
using FluentValidation;
using Mediator;

namespace Exparter.Application.Matching;

public sealed record SendSwapRequestCommand(
    Guid UserId,
    Guid RecipientId,
    Guid OfferedSkillId,
    Guid RequestedSkillId,
    string? Message) : ICommand<SwapRequestDto>;

public sealed class SendSwapRequestCommandValidator : AbstractValidator<SendSwapRequestCommand>
{
    public SendSwapRequestCommandValidator()
    {
        RuleFor(x => x.RecipientId).NotEmpty();
        RuleFor(x => x.OfferedSkillId).NotEmpty();
        RuleFor(x => x.RequestedSkillId).NotEmpty();
        RuleFor(x => x.Message).MaximumLength(SwapRequest.MessageMaxLength);
    }
}

public sealed class SendSwapRequestCommandHandler(
    IInteractionRequirements requirements,
    IUserDirectory users,
    IUserSkillDirectory skills,
    ISwapRequestRepository requests,
    IMatchRepository matches,
    MatchingDtoFactory dtos,
    IUserNotifier notifier,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<SendSwapRequestCommand, SwapRequestDto>
{
    public async ValueTask<SwapRequestDto> Handle(SendSwapRequestCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        await requirements.EnsureCanInteractAsync(command.UserId, cancellationToken);

        if (command.RecipientId != command.UserId)
        {
            if (await users.FindAsync(command.RecipientId, cancellationToken) is not { IsActive: true })
            {
                throw MatchingAppErrors.RecipientNotFound();
            }

            if (!await skills.OffersSkillAsync(command.UserId, command.OfferedSkillId, cancellationToken)
                || !await skills.OffersSkillAsync(command.RecipientId, command.RequestedSkillId, cancellationToken))
            {
                throw MatchingAppErrors.SkillNotOffered();
            }

            if (await matches.HasOpenBetweenAsync(command.UserId, command.RecipientId, cancellationToken))
            {
                throw MatchingAppErrors.MatchAlreadyExists();
            }
        }

        var lastRefused = await requests.FindLastRefusedAsync(command.UserId, command.RecipientId, cancellationToken);
        var pending = await requests.FindPendingBetweenAsync(command.UserId, command.RecipientId, cancellationToken);
        if (pending is not null)
        {
            if (pending.StatusAt(now) == SwapRequestStatus.Pending)
            {
                throw MatchingAppErrors.SwapRequestAlreadyPending();
            }

            // Its deadline passed but nothing recorded that yet. Record it now, and on its
            // own: the database allows one pending request per pair, so the old one has to
            // stop being pending before the new one is stored.
            pending.ExpireIfOverdue(now);
            notifier.Notify(pending.RequesterId, NotificationType.SwapRequestExpired, new NotificationData(SwapRequestId: pending.Id));
            await unitOfWork.SaveChangesAsync(cancellationToken);
            if (pending.RequesterId == command.UserId)
            {
                lastRefused = pending;
            }
        }

        var request = SwapRequest.Send(
            command.UserId,
            command.RecipientId,
            command.OfferedSkillId,
            command.RequestedSkillId,
            command.Message,
            lastRefused,
            now);
        requests.Add(request);
        notifier.Notify(
            request.RecipientId,
            NotificationType.SwapRequestReceived,
            new NotificationData(ActorUserId: command.UserId, SwapRequestId: request.Id));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await dtos.ToDtoAsync(request, cancellationToken);
    }
}

public sealed record ListSwapRequestsQuery(
    Guid UserId,
    SwapRequestDirection Direction,
    SwapRequestStatus? Status,
    int? Page,
    int? PageSize) : IQuery<PagedList<SwapRequestDto>>;

public sealed class ListSwapRequestsQueryValidator : AbstractValidator<ListSwapRequestsQuery>
{
    public ListSwapRequestsQueryValidator()
    {
        RuleFor(x => x.Direction).IsInEnum();
        RuleFor(x => x.Status).IsInEnum();
    }
}

public sealed class ListSwapRequestsQueryHandler(
    ISwapRequestRepository requests,
    MatchingDtoFactory dtos,
    TimeProvider timeProvider) : IQueryHandler<ListSwapRequestsQuery, PagedList<SwapRequestDto>>
{
    public async ValueTask<PagedList<SwapRequestDto>> Handle(ListSwapRequestsQuery query, CancellationToken cancellationToken)
    {
        var page = await requests.ListAsync(
            query.UserId,
            query.Direction,
            query.Status,
            timeProvider.GetUtcNow(),
            PageRequest.From(query.Page, query.PageSize),
            cancellationToken);
        return new PagedList<SwapRequestDto>(
            await dtos.ToDtosAsync(page.Items, cancellationToken),
            page.Page,
            page.PageSize,
            page.TotalCount);
    }
}

public sealed record GetSwapRequestQuery(Guid UserId, Guid SwapRequestId) : IQuery<SwapRequestDto>;

public sealed class GetSwapRequestQueryHandler(ISwapRequestRepository requests, MatchingDtoFactory dtos)
    : IQueryHandler<GetSwapRequestQuery, SwapRequestDto>
{
    public async ValueTask<SwapRequestDto> Handle(GetSwapRequestQuery query, CancellationToken cancellationToken) =>
        await dtos.ToDtoAsync(await requests.GetForParticipantAsync(query.SwapRequestId, query.UserId, cancellationToken), cancellationToken);
}

public sealed record AcceptSwapRequestCommand(Guid UserId, Guid SwapRequestId) : ICommand<MatchDto>;

public sealed class AcceptSwapRequestCommandHandler(
    IInteractionRequirements requirements,
    ISwapRequestRepository requests,
    IMatchRepository matches,
    MatchingDtoFactory dtos,
    IPublisher publisher,
    IUserNotifier notifier,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<AcceptSwapRequestCommand, MatchDto>
{
    public async ValueTask<MatchDto> Handle(AcceptSwapRequestCommand command, CancellationToken cancellationToken)
    {
        var request = await requests.GetForParticipantAsync(command.SwapRequestId, command.UserId, cancellationToken);
        await requirements.EnsureCanInteractAsync(command.UserId, cancellationToken);

        var match = request.Accept(command.UserId, timeProvider.GetUtcNow());
        matches.Add(match);
        await publisher.Publish(new MatchCreated(match.Id, match.UserAId, match.UserBId), cancellationToken);
        notifier.Notify(
            request.RequesterId,
            NotificationType.SwapRequestAccepted,
            new NotificationData(ActorUserId: command.UserId, SwapRequestId: request.Id, MatchId: match.Id));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await dtos.ToDtoAsync(match, command.UserId, cancellationToken);
    }
}

public sealed record DeclineSwapRequestCommand(Guid UserId, Guid SwapRequestId) : ICommand<SwapRequestDto>;

public sealed class DeclineSwapRequestCommandHandler(
    ISwapRequestRepository requests,
    MatchingDtoFactory dtos,
    IUserNotifier notifier,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<DeclineSwapRequestCommand, SwapRequestDto>
{
    public async ValueTask<SwapRequestDto> Handle(DeclineSwapRequestCommand command, CancellationToken cancellationToken)
    {
        var request = await requests.GetForParticipantAsync(command.SwapRequestId, command.UserId, cancellationToken);
        request.Decline(command.UserId, timeProvider.GetUtcNow());
        notifier.Notify(
            request.RequesterId,
            NotificationType.SwapRequestDeclined,
            new NotificationData(ActorUserId: command.UserId, SwapRequestId: request.Id));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await dtos.ToDtoAsync(request, cancellationToken);
    }
}

public sealed record CancelSwapRequestCommand(Guid UserId, Guid SwapRequestId) : ICommand<SwapRequestDto>;

public sealed class CancelSwapRequestCommandHandler(
    ISwapRequestRepository requests,
    MatchingDtoFactory dtos,
    IUserNotifier notifier,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<CancelSwapRequestCommand, SwapRequestDto>
{
    public async ValueTask<SwapRequestDto> Handle(CancelSwapRequestCommand command, CancellationToken cancellationToken)
    {
        var request = await requests.GetForParticipantAsync(command.SwapRequestId, command.UserId, cancellationToken);
        request.Cancel(command.UserId, timeProvider.GetUtcNow());
        notifier.Notify(
            request.RecipientId,
            NotificationType.SwapRequestCancelled,
            new NotificationData(ActorUserId: command.UserId, SwapRequestId: request.Id));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await dtos.ToDtoAsync(request, cancellationToken);
    }
}

internal static class MatchingRepositoryExtensions
{
    /// <summary>
    /// A request or match is visible only to its two users; to anyone else it does not
    /// exist, so that ids cannot be probed.
    /// </summary>
    public static async Task<SwapRequest> GetForParticipantAsync(
        this ISwapRequestRepository requests,
        Guid swapRequestId,
        Guid userId,
        CancellationToken cancellationToken) =>
        await requests.GetByIdAsync(swapRequestId, cancellationToken) is { } request && request.IsParticipant(userId)
            ? request
            : throw MatchingAppErrors.SwapRequestNotFound();

    /// <inheritdoc cref="GetForParticipantAsync(ISwapRequestRepository, Guid, Guid, CancellationToken)"/>
    public static async Task<Match> GetForParticipantAsync(
        this IMatchRepository matches,
        Guid matchId,
        Guid userId,
        CancellationToken cancellationToken) =>
        await matches.GetByIdAsync(matchId, cancellationToken) is { } match && match.IsParticipant(userId)
            ? match
            : throw MatchingAppErrors.MatchNotFound();
}
