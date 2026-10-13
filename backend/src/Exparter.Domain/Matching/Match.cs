namespace Exparter.Domain.Matching;

public enum MatchStatus
{
    Active,
    EndRequested,
    Ended,
}

public enum MatchEndReason
{
    MutualConsent,
    EndRequestExpired,
    Blocked,
    AccountDeleted,
}

/// <summary>
/// Two users swapping skills, created when a swap request is accepted. User A is the one
/// who sent that request.
/// </summary>
public sealed class Match
{
    /// <summary>How long the other user has to answer an end request before it takes effect.</summary>
    public static readonly TimeSpan EndRequestLifetime = TimeSpan.FromDays(7);

    /// <summary>How long before an end request takes effect the other user is reminded of it.</summary>
    public static readonly TimeSpan EndReminderLeadTime = TimeSpan.FromHours(24);

    private Match()
    {
    }

    public Guid Id { get; private set; }
    public Guid SwapRequestId { get; private set; }
    public Guid UserAId { get; private set; }
    public Guid UserBId { get; private set; }
    public Guid SkillTaughtByAId { get; private set; }
    public Guid SkillTaughtByBId { get; private set; }
    public MatchStatus Status { get; private set; }

    /// <summary>
    /// Who asked to end the match. Kept after the match has ended: it is part of the
    /// record that user statistics are later derived from.
    /// </summary>
    public Guid? EndRequestedByUserId { get; private set; }

    public DateTimeOffset? EndRequestedAt { get; private set; }
    public DateTimeOffset? EndRequestExpiresAt { get; private set; }
    public DateTimeOffset? EndReminderSentAt { get; private set; }
    public MatchEndReason? EndReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? EndedAt { get; private set; }

    internal static Match From(SwapRequest request, DateTimeOffset now) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            SwapRequestId = request.Id,
            UserAId = request.RequesterId,
            UserBId = request.RecipientId,
            SkillTaughtByAId = request.OfferedSkillId,
            SkillTaughtByBId = request.RequestedSkillId,
            Status = MatchStatus.Active,
            CreatedAt = now,
        };

    public bool IsParticipant(Guid userId) => userId == UserAId || userId == UserBId;

    public Guid OtherThan(Guid userId) => userId == UserAId ? UserBId : UserAId;

    /// <summary>One user asks to end the match; the other has to agree.</summary>
    public void RequestEnd(Guid userId, DateTimeOffset now)
    {
        EnsureParticipant(userId);
        if (Status != MatchStatus.Active)
        {
            throw MatchingErrors.InvalidState("Only an active match can be asked to end.");
        }

        Status = MatchStatus.EndRequested;
        EndRequestedByUserId = userId;
        EndRequestedAt = now;
        EndRequestExpiresAt = now + EndRequestLifetime;
        EndReminderSentAt = null;
    }

    public void WithdrawEndRequest(Guid userId)
    {
        EnsureEndRequested();
        if (userId != EndRequestedByUserId)
        {
            throw MatchingErrors.NotParticipant("Only the user who asked to end the match can withdraw that request.");
        }

        Reactivate();
    }

    public void AcceptEnd(Guid userId, DateTimeOffset now)
    {
        EnsureAnswersEndRequest(userId);
        End(MatchEndReason.MutualConsent, now);
    }

    public void RejectEnd(Guid userId)
    {
        EnsureAnswersEndRequest(userId);
        Reactivate();
    }

    /// <summary>An unanswered end request takes effect. Does nothing before its deadline.</summary>
    public void EndIfEndRequestOverdue(DateTimeOffset now)
    {
        if (Status == MatchStatus.EndRequested && now >= EndRequestExpiresAt)
        {
            End(MatchEndReason.EndRequestExpired, EndRequestExpiresAt!.Value);
        }
    }

    /// <summary>One of the two users is leaving: the match ends without anyone being asked.</summary>
    public void EndBecauseAccountDeleted(DateTimeOffset now)
    {
        if (Status != MatchStatus.Ended)
        {
            End(MatchEndReason.AccountDeleted, now);
        }
    }

    /// <summary>
    /// Says, once per end request and only in its last day, that the user who has not
    /// answered it should be reminded.
    /// </summary>
    public bool TakeDueEndReminder(DateTimeOffset now)
    {
        if (Status != MatchStatus.EndRequested
            || EndReminderSentAt is not null
            || now >= EndRequestExpiresAt
            || now < EndRequestExpiresAt - EndReminderLeadTime)
        {
            return false;
        }

        EndReminderSentAt = now;
        return true;
    }

    private void EnsureParticipant(Guid userId)
    {
        if (!IsParticipant(userId))
        {
            throw MatchingErrors.NotParticipant("The user is not part of this match.");
        }
    }

    private void EnsureEndRequested()
    {
        if (Status != MatchStatus.EndRequested)
        {
            throw MatchingErrors.InvalidState("The match has no pending end request.");
        }
    }

    private void EnsureAnswersEndRequest(Guid userId)
    {
        EnsureParticipant(userId);
        EnsureEndRequested();
        if (userId == EndRequestedByUserId)
        {
            throw MatchingErrors.NotParticipant("Only the other user can answer an end request.");
        }
    }

    private void Reactivate()
    {
        Status = MatchStatus.Active;
        EndRequestedByUserId = null;
        EndRequestedAt = null;
        EndRequestExpiresAt = null;
    }

    private void End(MatchEndReason reason, DateTimeOffset at)
    {
        Status = MatchStatus.Ended;
        EndReason = reason;
        EndedAt = at;
        EndRequestExpiresAt = null;
    }
}
