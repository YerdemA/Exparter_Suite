using Exparter.Application.Common;
using Exparter.Application.Messaging;
using Exparter.Application.Skills;
using Exparter.Domain.Matching;

namespace Exparter.Application.Matching;

public enum SwapRequestDirection
{
    /// <summary>Requests the user received.</summary>
    Incoming,

    /// <summary>Requests the user sent.</summary>
    Outgoing,
}

/// <summary>
/// Methods that take <c>now</c> treat a pending request past its deadline as expired,
/// whether or not that has been recorded yet.
/// </summary>
public interface ISwapRequestRepository
{
    Task<SwapRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The request stored as pending between two users, in either direction.</summary>
    Task<SwapRequest?> FindPendingBetweenAsync(Guid firstUserId, Guid secondUserId, CancellationToken cancellationToken);

    /// <summary>The requester's most recent request to the recipient that was declined or recorded as expired.</summary>
    Task<SwapRequest?> FindLastRefusedAsync(Guid requesterId, Guid recipientId, CancellationToken cancellationToken);

    /// <summary>Newest first.</summary>
    Task<PagedList<SwapRequest>> ListAsync(
        Guid userId,
        SwapRequestDirection direction,
        SwapRequestStatus? status,
        DateTimeOffset now,
        PageRequest page,
        CancellationToken cancellationToken);

    /// <summary>Pending requests past their deadline, oldest first.</summary>
    Task<IReadOnlyList<SwapRequest>> ListOverdueAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken);

    /// <summary>Whether a pending request relies on the user teaching this skill.</summary>
    Task<bool> UsesOfferedSkillAsync(Guid userId, Guid skillId, DateTimeOffset now, CancellationToken cancellationToken);

    void Add(SwapRequest request);
}

public interface IMatchRepository
{
    Task<Match?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Whether the two users have a match that has not ended.</summary>
    Task<bool> HasOpenBetweenAsync(Guid firstUserId, Guid secondUserId, CancellationToken cancellationToken);

    /// <summary>Newest first.</summary>
    Task<PagedList<Match>> ListAsync(Guid userId, MatchStatus? status, PageRequest page, CancellationToken cancellationToken);

    /// <summary>Every match of the user, in no particular order.</summary>
    Task<IReadOnlyList<Match>> ListAllAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Matches whose end request was not answered in time, oldest first.</summary>
    Task<IReadOnlyList<Match>> ListWithOverdueEndRequestAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken);

    /// <summary>Matches with an end request in its last day that nobody was reminded of yet.</summary>
    Task<IReadOnlyList<Match>> ListDueForEndReminderAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken);

    /// <summary>Whether a match that has not ended relies on the user teaching this skill.</summary>
    Task<bool> UsesOfferedSkillAsync(Guid userId, Guid skillId, CancellationToken cancellationToken);

    /// <summary>Those of the other users with whom the user has a match that has not ended.</summary>
    Task<IReadOnlySet<Guid>> ListOpenPartnersAsync(
        Guid userId,
        IReadOnlyCollection<Guid> otherUserIds,
        CancellationToken cancellationToken);

    void Add(Match match);
}

/// <summary>
/// Published while a new match is being saved, so that handlers in other modules take
/// part in the same transaction.
/// </summary>
public sealed record MatchCreated(Guid MatchId, Guid UserAId, Guid UserBId) : Mediator.INotification;

/// <summary>
/// Published while a match is being ended, however it ends, so that handlers in other
/// modules take part in the same transaction.
/// </summary>
public sealed record MatchEnded(Guid MatchId) : Mediator.INotification;

/// <param name="IsActive">Running, with no request to end it pending.</param>
public sealed record MatchInfo(Guid Id, Guid UserAId, Guid UserBId, bool IsActive, bool HasPendingEndRequest)
{
    public bool IsParticipant(Guid userId) => userId == UserAId || userId == UserBId;

    public Guid OtherThan(Guid userId) => userId == UserAId ? UserBId : UserAId;
}

public sealed record MatchSummary(Guid Id, bool IsEnded);

/// <summary>What other modules may know about matches.</summary>
public interface IMatchDirectory
{
    Task<MatchInfo?> FindAsync(Guid matchId, CancellationToken cancellationToken);

    /// <summary>Every match the user has had, in no particular order.</summary>
    Task<IReadOnlyList<MatchSummary>> ListForUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Those of the other users with whom the user has a match that has not ended.</summary>
    Task<IReadOnlySet<Guid>> GetUsersWithOpenMatchAsync(
        Guid userId,
        IReadOnlyCollection<Guid> otherUserIds,
        CancellationToken cancellationToken);
}

internal sealed class MatchDirectory(IMatchRepository matches) : IMatchDirectory
{
    public async Task<MatchInfo?> FindAsync(Guid matchId, CancellationToken cancellationToken) =>
        await matches.GetByIdAsync(matchId, cancellationToken) is { } match
            ? new MatchInfo(
                match.Id,
                match.UserAId,
                match.UserBId,
                match.Status == MatchStatus.Active,
                match.Status == MatchStatus.EndRequested)
            : null;

    public async Task<IReadOnlyList<MatchSummary>> ListForUserAsync(Guid userId, CancellationToken cancellationToken) =>
        (await matches.ListAllAsync(userId, cancellationToken))
        .Select(match => new MatchSummary(match.Id, match.Status == MatchStatus.Ended))
        .ToList();

    public Task<IReadOnlySet<Guid>> GetUsersWithOpenMatchAsync(
        Guid userId,
        IReadOnlyCollection<Guid> otherUserIds,
        CancellationToken cancellationToken) =>
        matches.ListOpenPartnersAsync(userId, otherUserIds, cancellationToken);
}

/// <summary>What other modules may know about how skills are used in requests and matches.</summary>
public interface ISkillUsage
{
    /// <summary>Whether a pending request or an unfinished match relies on the user teaching this skill.</summary>
    Task<bool> IsOfferedSkillInUseAsync(Guid userId, Guid skillId, CancellationToken cancellationToken);
}

internal sealed class SkillUsage(ISwapRequestRepository requests, IMatchRepository matches, TimeProvider timeProvider) : ISkillUsage
{
    public async Task<bool> IsOfferedSkillInUseAsync(Guid userId, Guid skillId, CancellationToken cancellationToken) =>
        await requests.UsesOfferedSkillAsync(userId, skillId, timeProvider.GetUtcNow(), cancellationToken)
        || await matches.UsesOfferedSkillAsync(userId, skillId, cancellationToken);
}

public sealed record SwapRequestDto(
    Guid Id,
    UserSummaryDto Requester,
    UserSummaryDto Recipient,
    SkillRefDto OfferedSkill,
    SkillRefDto RequestedSkill,
    string? Message,
    SwapRequestStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? RespondedAt);

/// <param name="ConversationId">The conversation between the two users; every match has one.</param>
public sealed record MatchDto(
    Guid Id,
    Guid SwapRequestId,
    UserSummaryDto UserA,
    UserSummaryDto UserB,
    SkillRefDto SkillTaughtByA,
    SkillRefDto SkillTaughtByB,
    MatchStatus Status,
    Guid? ConversationId,
    Guid? EndRequestedByUserId,
    DateTimeOffset? EndRequestedAt,
    DateTimeOffset? EndRequestExpiresAt,
    MatchEndReason? EndReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? EndedAt);

/// <summary>Turns requests and matches into what the API returns, looking up the users and skills they refer to.</summary>
public sealed class MatchingDtoFactory(
    IUserSummaries users,
    IUserSkillDirectory skills,
    IConversationDirectory conversations,
    TimeProvider timeProvider)
{
    private const string UnknownSkillName = "";

    public async Task<SwapRequestDto> ToDtoAsync(SwapRequest request, CancellationToken cancellationToken) =>
        (await ToDtosAsync([request], cancellationToken))[0];

    public async Task<IReadOnlyList<SwapRequestDto>> ToDtosAsync(
        IReadOnlyList<SwapRequest> requests,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var summaries = await users.GetAsync(
            requests.SelectMany(request => new[] { request.RequesterId, request.RecipientId }).ToList(),
            cancellationToken);
        var names = await skills.GetSkillNamesAsync(
            requests.SelectMany(request => new[] { request.OfferedSkillId, request.RequestedSkillId }).Distinct().ToList(),
            cancellationToken);

        return requests.Select(request => new SwapRequestDto(
                request.Id,
                summaries[request.RequesterId],
                summaries[request.RecipientId],
                Skill(names, request.OfferedSkillId),
                Skill(names, request.RequestedSkillId),
                request.Message,
                request.StatusAt(now),
                request.CreatedAt,
                request.ExpiresAt,
                request.RespondedAt))
            .ToList();
    }

    /// <param name="viewerId">One of the match's two users.</param>
    public async Task<MatchDto> ToDtoAsync(Match match, Guid viewerId, CancellationToken cancellationToken) =>
        (await ToDtosAsync([match], viewerId, cancellationToken))[0];

    /// <param name="viewerId">A user who is part of every one of the matches.</param>
    public async Task<IReadOnlyList<MatchDto>> ToDtosAsync(
        IReadOnlyList<Match> matches,
        Guid viewerId,
        CancellationToken cancellationToken)
    {
        static Guid Other(Match match, Guid viewerId) => match.UserAId == viewerId ? match.UserBId : match.UserAId;

        var conversationIds = await conversations.FindIdsByOtherUserAsync(
            viewerId,
            matches.Select(match => Other(match, viewerId)).Distinct().ToList(),
            cancellationToken);
        var summaries = await users.GetAsync(
            matches.SelectMany(match => new[] { match.UserAId, match.UserBId }).ToList(),
            cancellationToken);
        var names = await skills.GetSkillNamesAsync(
            matches.SelectMany(match => new[] { match.SkillTaughtByAId, match.SkillTaughtByBId }).Distinct().ToList(),
            cancellationToken);

        return matches.Select(match => new MatchDto(
                match.Id,
                match.SwapRequestId,
                summaries[match.UserAId],
                summaries[match.UserBId],
                Skill(names, match.SkillTaughtByAId),
                Skill(names, match.SkillTaughtByBId),
                match.Status,
                conversationIds.TryGetValue(Other(match, viewerId), out var conversationId) ? conversationId : null,
                match.EndRequestedByUserId,
                match.EndRequestedAt,
                match.EndRequestExpiresAt,
                match.EndReason,
                match.CreatedAt,
                match.EndedAt))
            .ToList();
    }

    private static SkillRefDto Skill(IReadOnlyDictionary<Guid, string> names, Guid skillId) =>
        new(skillId, names.GetValueOrDefault(skillId, UnknownSkillName));
}

internal static class MatchingAppErrors
{
    public static AppException SwapRequestNotFound() => AppException.NotFound("The swap request was not found.");

    public static AppException MatchNotFound() => AppException.NotFound("The match was not found.");

    public static AppException RecipientNotFound() => AppException.NotFound("The user was not found.");

    public static AppException SkillNotOffered() =>
        AppException.Conflict("skill_not_offered", "The chosen skill is not among the skills that user offers.");

    public static AppException SwapRequestAlreadyPending() =>
        AppException.Conflict("swap_request_already_pending", "The two users already have a pending swap request.");

    public static AppException MatchAlreadyExists() =>
        AppException.Conflict("match_already_exists", "The two users already have a match that has not ended.");
}
