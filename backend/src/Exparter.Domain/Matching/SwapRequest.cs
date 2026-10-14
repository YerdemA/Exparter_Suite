using Exparter.Domain.Common;

namespace Exparter.Domain.Matching;

public enum SwapRequestStatus
{
    Pending,
    Accepted,
    Declined,
    Cancelled,
    Expired,
}

public static class MatchingErrors
{
    public const string SwapRequestToSelfCode = "swap_request_to_self";
    public const string SwapRequestCooldownCode = "swap_request_cooldown";
    public const string InvalidStateCode = "invalid_state";
    public const string NotParticipantCode = DomainErrorCodes.NotParticipant;

    public static DomainException SwapRequestToSelf() =>
        new(SwapRequestToSelfCode, "A user cannot send a swap request to themselves.");

    public static DomainException SwapRequestCooldown() =>
        new(
            SwapRequestCooldownCode,
            $"A new request to the same user can be sent {SwapRequest.ResendCooldown.Days} days after one was declined or expired.");

    public static DomainException InvalidState(string message) => new(InvalidStateCode, message);

    public static DomainException NotParticipant(string message) => new(NotParticipantCode, message);
}

/// <summary>
/// One user's proposal to swap skills with another: the requester teaches
/// <see cref="OfferedSkillId"/> and learns <see cref="RequestedSkillId"/>.
/// </summary>
public sealed class SwapRequest
{
    public const int MessageMaxLength = 300;

    /// <summary>How long the recipient has to answer.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(3);

    /// <summary>
    /// How long a requester waits before asking the same user again after being declined
    /// or ignored, so that saying no is not answered with another request right away.
    /// </summary>
    public static readonly TimeSpan ResendCooldown = TimeSpan.FromDays(7);

    private SwapRequest()
    {
    }

    public Guid Id { get; private set; }
    public Guid RequesterId { get; private set; }
    public Guid RecipientId { get; private set; }
    public Guid OfferedSkillId { get; private set; }
    public Guid RequestedSkillId { get; private set; }
    public string? Message { get; private set; }

    /// <summary>
    /// The stored status. A pending request past <see cref="ExpiresAt"/> stays
    /// <see cref="SwapRequestStatus.Pending"/> here until it is processed; read
    /// <see cref="StatusAt"/> for what it means at a given moment.
    /// </summary>
    public SwapRequestStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>When the request left the pending state, however it did.</summary>
    public DateTimeOffset? RespondedAt { get; private set; }

    /// <param name="lastRefused">
    /// The requester's most recent declined or expired request to the same recipient, if any.
    /// </param>
    public static SwapRequest Send(
        Guid requesterId,
        Guid recipientId,
        Guid offeredSkillId,
        Guid requestedSkillId,
        string? message,
        SwapRequest? lastRefused,
        DateTimeOffset now)
    {
        if (requesterId == recipientId)
        {
            throw MatchingErrors.SwapRequestToSelf();
        }

        if (lastRefused is not null && now < lastRefused.RefusedAt(now) + ResendCooldown)
        {
            throw MatchingErrors.SwapRequestCooldown();
        }

        var trimmed = message?.Trim();
        return new SwapRequest
        {
            Id = Guid.CreateVersion7(),
            RequesterId = requesterId,
            RecipientId = recipientId,
            OfferedSkillId = offeredSkillId,
            RequestedSkillId = requestedSkillId,
            Message = string.IsNullOrEmpty(trimmed) ? null : trimmed,
            Status = SwapRequestStatus.Pending,
            CreatedAt = now,
            ExpiresAt = now + Lifetime,
        };
    }

    public bool IsParticipant(Guid userId) => userId == RequesterId || userId == RecipientId;

    public SwapRequestStatus StatusAt(DateTimeOffset now) =>
        Status == SwapRequestStatus.Pending && now >= ExpiresAt ? SwapRequestStatus.Expired : Status;

    /// <summary>The recipient accepts; the result is the match between the two users.</summary>
    public Match Accept(Guid userId, DateTimeOffset now)
    {
        EnsureIs(RecipientId, userId, "Only the recipient can accept a swap request.");
        Close(SwapRequestStatus.Accepted, now);
        return Match.From(this, now);
    }

    public void Decline(Guid userId, DateTimeOffset now)
    {
        EnsureIs(RecipientId, userId, "Only the recipient can decline a swap request.");
        Close(SwapRequestStatus.Declined, now);
    }

    public void Cancel(Guid userId, DateTimeOffset now)
    {
        EnsureIs(RequesterId, userId, "Only the requester can withdraw a swap request.");
        Close(SwapRequestStatus.Cancelled, now);
    }

    /// <summary>One of the two users is leaving: a request still waiting for an answer is withdrawn.</summary>
    public void CancelBecauseAccountDeleted(DateTimeOffset now) => CancelUnanswered(now);

    /// <summary>One of the two users blocked the other: a request still waiting for an answer is withdrawn.</summary>
    public void CancelBecauseBlocked(DateTimeOffset now) => CancelUnanswered(now);

    private void CancelUnanswered(DateTimeOffset now)
    {
        if (StatusAt(now) == SwapRequestStatus.Pending)
        {
            Status = SwapRequestStatus.Cancelled;
            RespondedAt = now;
        }
    }

    /// <summary>Records that the time to answer has run out. Does nothing before then.</summary>
    public void ExpireIfOverdue(DateTimeOffset now)
    {
        if (Status == SwapRequestStatus.Pending && now >= ExpiresAt)
        {
            Status = SwapRequestStatus.Expired;
            RespondedAt = ExpiresAt;
        }
    }

    private static void EnsureIs(Guid expected, Guid userId, string message)
    {
        if (userId != expected)
        {
            throw MatchingErrors.NotParticipant(message);
        }
    }

    private DateTimeOffset RefusedAt(DateTimeOffset now) =>
        StatusAt(now) == SwapRequestStatus.Expired ? ExpiresAt : RespondedAt ?? CreatedAt;

    private void Close(SwapRequestStatus status, DateTimeOffset now)
    {
        if (StatusAt(now) != SwapRequestStatus.Pending)
        {
            throw MatchingErrors.InvalidState("Only a pending swap request that has not expired can be answered or withdrawn.");
        }

        Status = status;
        RespondedAt = now;
    }
}
