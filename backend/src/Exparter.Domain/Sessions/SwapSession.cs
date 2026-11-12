using Exparter.Domain.Common;

namespace Exparter.Domain.Sessions;

public enum SwapSessionStatus
{
    Proposed,
    Scheduled,
    CompletionPending,
    Completed,
    Declined,
    Cancelled,
}

public static class SessionErrors
{
    public const string InvalidStateCode = "invalid_state";
    public const string SessionInPastCode = "session_in_past";
    public const string SessionNotStartedCode = "session_not_started";
    public const string InvalidSessionCode = "invalid_session";
    public const string CallNotOpenYetCode = "call_not_open_yet";
    public const string CallWindowClosedCode = "call_window_closed";

    public static DomainException InvalidState(string message) => new(InvalidStateCode, message);

    public static DomainException SessionInPast() => new(SessionInPastCode, "A session must be scheduled in the future.");

    public static DomainException SessionNotStarted() =>
        new(SessionNotStartedCode, "A session can be marked as completed only after its scheduled start.");

    public static DomainException InvalidSession(string message) => new(InvalidSessionCode, message);

    public static DomainException NotParticipant(string message) => new(DomainErrorCodes.NotParticipant, message);

    public static DomainException CallNotOpenYet() =>
        new(CallNotOpenYetCode, $"The call can be joined from {SwapSession.CallOpensBefore.TotalMinutes} minutes before the start.");

    public static DomainException CallWindowClosed() => new(CallWindowClosedCode, "The call of this session has closed.");
}

/// <summary>
/// One online meeting within a match, planned in the app: proposed, accepted, held,
/// then marked and confirmed as completed.
/// </summary>
public sealed class SwapSession
{
    public const int NoteMaxLength = 500;
    public const int CancelReasonMinLength = 5;
    public const int CancelReasonMaxLength = 500;

    /// <summary>The lengths, in minutes, an online session can be planned for.</summary>
    public static readonly IReadOnlyList<int> AllowedDurations = [30, 45, 60, 90, 120];

    /// <summary>How long the other user has to confirm or dispute a completion mark before it stands.</summary>
    public static readonly TimeSpan ConfirmationPeriod = TimeSpan.FromHours(72);

    /// <summary>How long before a completion mark stands the other user is reminded of it.</summary>
    public static readonly TimeSpan CompletionReminderLeadTime = TimeSpan.FromHours(24);

    /// <summary>How long before a scheduled session starts its two users are reminded: a day and an hour ahead.</summary>
    public static readonly TimeSpan DayReminderLeadTime = TimeSpan.FromHours(24);

    /// <inheritdoc cref="DayReminderLeadTime"/>
    public static readonly TimeSpan HourReminderLeadTime = TimeSpan.FromHours(1);

    /// <summary>How long before its start the video call of an online session can be joined.</summary>
    public static readonly TimeSpan CallOpensBefore = TimeSpan.FromMinutes(10);

    /// <summary>How long after its planned end the video call stays open, so that a late or long session is not cut off.</summary>
    public static readonly TimeSpan CallClosesAfter = TimeSpan.FromMinutes(15);

    private SwapSession()
    {
    }

    public Guid Id { get; private set; }
    public Guid MatchId { get; private set; }

    public Guid ProposedByUserId { get; private set; }
    public Guid OtherUserId { get; private set; }
    public DateTimeOffset ScheduledAt { get; private set; }
    public int DurationMinutes { get; private set; }
    public string? Note { get; private set; }

    /// <summary>
    /// The stored status. A proposal past its start time and a completion mark past its
    /// deadline stay as they are here until they are processed; read
    /// <see cref="StatusAt"/> for what they mean at a given moment.
    /// </summary>
    public SwapSessionStatus Status { get; private set; }

    public Guid? CompletionMarkedByUserId { get; private set; }
    public DateTimeOffset? CompletionMarkedAt { get; private set; }
    public DateTimeOffset? CompletionConfirmDeadline { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>True when nobody answered the completion mark in time and it stood by default.</summary>
    public bool CompletedAutomatically { get; private set; }

    /// <summary>Null when the session was cancelled because its match ended.</summary>
    public Guid? CancelledByUserId { get; private set; }

    public string? CancelReason { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? DayReminderSentAt { get; private set; }
    public DateTimeOffset? HourReminderSentAt { get; private set; }
    public DateTimeOffset? CompletionReminderSentAt { get; private set; }

    public static SwapSession Propose(
        Guid matchId,
        Guid proposerId,
        Guid otherUserId,
        DateTimeOffset scheduledAt,
        int durationMinutes,
        string? note,
        DateTimeOffset now)
    {
        if (scheduledAt <= now)
        {
            throw SessionErrors.SessionInPast();
        }

        if (!AllowedDurations.Contains(durationMinutes))
        {
            throw SessionErrors.InvalidSession($"The duration must be one of {string.Join(", ", AllowedDurations)} minutes.");
        }

        var trimmed = note?.Trim();
        if (trimmed?.Length > NoteMaxLength)
        {
            throw SessionErrors.InvalidSession($"The note can be at most {NoteMaxLength} characters.");
        }

        return new SwapSession
        {
            Id = Guid.CreateVersion7(),
            MatchId = matchId,
            ProposedByUserId = proposerId,
            OtherUserId = otherUserId,
            ScheduledAt = scheduledAt,
            DurationMinutes = durationMinutes,
            Note = string.IsNullOrEmpty(trimmed) ? null : trimmed,
            Status = SwapSessionStatus.Proposed,
            CreatedAt = now,
        };
    }

    public DateTimeOffset CallOpensAt => ScheduledAt - CallOpensBefore;

    public DateTimeOffset CallClosesAt => EndsAt + CallClosesAfter;

    /// <summary>
    /// Whether the call can be in progress: the session was accepted and not cancelled,
    /// and the window is open. Marking it completed does not end the call, since users
    /// often do that before hanging up.
    /// </summary>
    public bool IsCallOpenAt(DateTimeOffset now) =>
        HasCall(now) && now >= CallOpensAt && now < CallClosesAt;

    public void EnsureCanJoinCall(Guid userId, DateTimeOffset now)
    {
        EnsureParticipant(userId);
        if (!HasCall(now))
        {
            throw SessionErrors.InvalidState("Only an accepted online session that was not cancelled has a call.");
        }

        if (now < CallOpensAt)
        {
            throw SessionErrors.CallNotOpenYet();
        }

        if (now >= CallClosesAt)
        {
            throw SessionErrors.CallWindowClosed();
        }
    }

    public bool IsParticipant(Guid userId) => userId == ProposedByUserId || userId == OtherUserId;

    public Guid OtherThan(Guid userId) => userId == ProposedByUserId ? OtherUserId : ProposedByUserId;

    public DateTimeOffset EndsAt => ScheduledAt + TimeSpan.FromMinutes(DurationMinutes);

    /// <summary>Whether the session's planned time and the given one have any moment in common.</summary>
    public bool OverlapsWith(DateTimeOffset start, DateTimeOffset end) =>
        ScheduledAt < end && start < EndsAt;

    /// <summary>
    /// Whether the session keeps the user from planning another one at the same time:
    /// once accepted it does, until it is cancelled; as an open proposal it does only for
    /// the user who made it, since the other user has not agreed to it.
    /// </summary>
    public bool OccupiesTimeOf(Guid userId, DateTimeOffset now) =>
        IsParticipant(userId)
        && StatusAt(now) switch
        {
            SwapSessionStatus.Scheduled or SwapSessionStatus.CompletionPending or SwapSessionStatus.Completed => true,
            SwapSessionStatus.Proposed => userId == ProposedByUserId,
            _ => false,
        };

    public SwapSessionStatus StatusAt(DateTimeOffset now) => Status switch
    {
        SwapSessionStatus.Proposed when now >= ScheduledAt => SwapSessionStatus.Declined,
        SwapSessionStatus.CompletionPending when now >= CompletionConfirmDeadline => SwapSessionStatus.Completed,
        _ => Status,
    };

    public void Accept(Guid userId, DateTimeOffset now)
    {
        EnsureAnswersProposal(userId, now);
        Status = SwapSessionStatus.Scheduled;

        // Accepted inside a reminder's lead time: the acceptance itself is the news, and
        // a reminder right after it would only repeat it.
        if (ScheduledAt - now <= DayReminderLeadTime)
        {
            DayReminderSentAt = now;
        }

        if (ScheduledAt - now <= HourReminderLeadTime)
        {
            HourReminderSentAt = now;
        }
    }

    public void Decline(Guid userId, DateTimeOffset now)
    {
        EnsureAnswersProposal(userId, now);
        Status = SwapSessionStatus.Declined;
    }

    /// <summary>Either user cancels, alone, and says why. This is also how a completion mark is disputed.</summary>
    public void Cancel(Guid userId, string reason, DateTimeOffset now)
    {
        EnsureParticipant(userId);
        var trimmed = reason.Trim();
        if (trimmed.Length is < CancelReasonMinLength or > CancelReasonMaxLength)
        {
            throw SessionErrors.InvalidSession(
                $"The reason must be {CancelReasonMinLength} to {CancelReasonMaxLength} characters.");
        }

        if (StatusAt(now) is not (SwapSessionStatus.Proposed or SwapSessionStatus.Scheduled or SwapSessionStatus.CompletionPending))
        {
            throw SessionErrors.InvalidState("Only a session that is not finished yet can be cancelled.");
        }

        Status = SwapSessionStatus.Cancelled;
        CancelledByUserId = userId;
        CancelReason = trimmed;
        CancelledAt = now;
        CompletionConfirmDeadline = null;
    }

    /// <summary>The match ended: sessions that were still ahead will not take place.</summary>
    public void CancelBecauseMatchEnded(DateTimeOffset now)
    {
        if (StatusAt(now) is SwapSessionStatus.Proposed or SwapSessionStatus.Scheduled)
        {
            Status = SwapSessionStatus.Cancelled;
            CancelledAt = now;
        }
    }

    public void MarkCompleted(Guid userId, DateTimeOffset now)
    {
        EnsureParticipant(userId);
        if (Status != SwapSessionStatus.Scheduled)
        {
            throw SessionErrors.InvalidState("Only a scheduled session can be marked as completed.");
        }

        if (now < ScheduledAt)
        {
            throw SessionErrors.SessionNotStarted();
        }

        Status = SwapSessionStatus.CompletionPending;
        CompletionMarkedByUserId = userId;
        CompletionMarkedAt = now;
        CompletionConfirmDeadline = now + ConfirmationPeriod;
    }

    /// <summary>
    /// Says, once for each of the two lead times, that the users of a scheduled session
    /// should be reminded of its start.
    /// </summary>
    public bool TakeDueStartReminder(DateTimeOffset now)
    {
        if (Status != SwapSessionStatus.Scheduled || now >= ScheduledAt)
        {
            return false;
        }

        if (HourReminderSentAt is null && now >= ScheduledAt - HourReminderLeadTime)
        {
            // The hour reminder also stands in for a day reminder that was never sent.
            HourReminderSentAt = now;
            DayReminderSentAt ??= now;
            return true;
        }

        if (DayReminderSentAt is null && now >= ScheduledAt - DayReminderLeadTime)
        {
            DayReminderSentAt = now;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Says, once and only in the last day before a completion mark stands, that the
    /// user who has not answered it should be reminded.
    /// </summary>
    public bool TakeDueCompletionReminder(DateTimeOffset now)
    {
        if (Status != SwapSessionStatus.CompletionPending
            || CompletionReminderSentAt is not null
            || now >= CompletionConfirmDeadline
            || now < CompletionConfirmDeadline - CompletionReminderLeadTime)
        {
            return false;
        }

        CompletionReminderSentAt = now;
        return true;
    }

    public void ConfirmCompleted(Guid userId, DateTimeOffset now)
    {
        EnsureParticipant(userId);
        if (StatusAt(now) != SwapSessionStatus.CompletionPending)
        {
            throw SessionErrors.InvalidState("The session is not waiting for a completion to be confirmed.");
        }

        if (userId == CompletionMarkedByUserId)
        {
            throw SessionErrors.NotParticipant("Only the other user can confirm a completion.");
        }

        Status = SwapSessionStatus.Completed;
        CompletedAt = now;
        CompletionConfirmDeadline = null;
    }

    /// <summary>Records a deadline that has passed. Does nothing before then.</summary>
    public void RecordPassedDeadline(DateTimeOffset now)
    {
        if (Status == StatusAt(now))
        {
            return;
        }

        if (Status == SwapSessionStatus.Proposed)
        {
            Status = SwapSessionStatus.Declined;
        }
        else
        {
            Status = SwapSessionStatus.Completed;
            CompletedAt = CompletionConfirmDeadline;
            CompletedAutomatically = true;
            CompletionConfirmDeadline = null;
        }
    }

    private bool HasCall(DateTimeOffset now) =>
        StatusAt(now) is SwapSessionStatus.Scheduled or SwapSessionStatus.CompletionPending or SwapSessionStatus.Completed;

    private void EnsureParticipant(Guid userId)
    {
        if (!IsParticipant(userId))
        {
            throw SessionErrors.NotParticipant("The user is not part of this session.");
        }
    }

    private void EnsureAnswersProposal(Guid userId, DateTimeOffset now)
    {
        EnsureParticipant(userId);
        if (StatusAt(now) != SwapSessionStatus.Proposed)
        {
            throw SessionErrors.InvalidState("Only a proposal whose start time has not passed can be answered.");
        }

        if (userId == ProposedByUserId)
        {
            throw SessionErrors.NotParticipant("Only the other user can answer a session proposal.");
        }
    }
}
