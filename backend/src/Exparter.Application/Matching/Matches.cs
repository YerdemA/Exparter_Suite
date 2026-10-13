using Exparter.Application.Common;
using Exparter.Domain.Matching;
using FluentValidation;
using Mediator;

namespace Exparter.Application.Matching;

public sealed record ListMatchesQuery(Guid UserId, MatchStatus? Status, int? Page, int? PageSize) : IQuery<PagedList<MatchDto>>;

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
        var page = await matches.ListAsync(query.UserId, query.Status, PageRequest.From(query.Page, query.PageSize), cancellationToken);
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

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await dtos.ToDtoAsync(match, command.UserId, cancellationToken);
    }
}

/// <summary>Records the deadlines that have passed: unanswered swap requests and end requests.</summary>
public sealed record ProcessMatchingDeadlinesCommand : ICommand<int>;

public sealed class ProcessMatchingDeadlinesCommandHandler(
    ISwapRequestRepository requests,
    IMatchRepository matches,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<ProcessMatchingDeadlinesCommand, int>
{
    /// <summary>Per run; whatever is left over is picked up by the next run.</summary>
    private const int BatchSize = 200;

    /// <returns>How many requests and matches were updated.</returns>
    public async ValueTask<int> Handle(ProcessMatchingDeadlinesCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var overdueRequests = await requests.ListOverdueAsync(now, BatchSize, cancellationToken);
        foreach (var request in overdueRequests)
        {
            request.ExpireIfOverdue(now);
        }

        var overdueMatches = await matches.ListWithOverdueEndRequestAsync(now, BatchSize, cancellationToken);
        foreach (var match in overdueMatches)
        {
            match.EndIfEndRequestOverdue(now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return overdueRequests.Count + overdueMatches.Count;
    }
}
