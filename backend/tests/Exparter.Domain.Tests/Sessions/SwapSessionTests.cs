using Exparter.Domain.Common;
using Exparter.Domain.Sessions;

namespace Exparter.Domain.Tests.Sessions;

public class SwapSessionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Start = Now.AddDays(2);
    private static readonly Guid MatchId = Guid.NewGuid();
    private static readonly Guid Ali = Guid.NewGuid();
    private static readonly Guid Ayse = Guid.NewGuid();

    private static SwapSession Proposed(string? note = null, int duration = 60) =>
        SwapSession.ProposeOnline(MatchId, Ali, Ayse, Start, duration, note, Now);

    private static SwapSession Scheduled()
    {
        var session = Proposed();
        session.Accept(Ayse, Now);
        return session;
    }

    private static string CodeOf(Action action) => Assert.Throws<DomainException>(action).Code;

    [Fact]
    public void An_online_session_starts_as_a_proposal_with_a_time_and_a_duration()
    {
        var session = Proposed("  https://meet.example/abc  ");

        Assert.Equal(SwapSessionStatus.Proposed, session.Status);
        Assert.Equal(SessionMode.Online, session.Mode);
        Assert.Equal((Start, 60), (session.ScheduledAt, session.DurationMinutes));
        Assert.Equal("https://meet.example/abc", session.Note);
        Assert.Equal((Ali, Ayse), (session.ProposedByUserId, session.OtherUserId));
        Assert.True(session.IsParticipant(Ayse));
        Assert.False(session.IsParticipant(Guid.NewGuid()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_session_cannot_be_proposed_for_now_or_the_past(int minutesFromNow) =>
        Assert.Equal(
            SessionErrors.SessionInPastCode,
            CodeOf(() => SwapSession.ProposeOnline(MatchId, Ali, Ayse, Now.AddMinutes(minutesFromNow), 60, null, Now)));

    [Theory]
    [InlineData(30, true)]
    [InlineData(45, true)]
    [InlineData(60, true)]
    [InlineData(90, true)]
    [InlineData(120, true)]
    [InlineData(15, false)]
    [InlineData(75, false)]
    [InlineData(180, false)]
    public void Only_the_offered_durations_are_accepted(int minutes, bool allowed)
    {
        if (allowed)
        {
            Assert.Equal(minutes, Proposed(duration: minutes).DurationMinutes);
        }
        else
        {
            Assert.Equal(SessionErrors.InvalidSessionCode, CodeOf(() => Proposed(duration: minutes)));
        }
    }

    [Fact]
    public void A_blank_note_is_not_stored_and_a_long_one_is_rejected()
    {
        Assert.Null(Proposed("   ").Note);
        Assert.Equal(SessionErrors.InvalidSessionCode, CodeOf(() => Proposed(new string('a', 501))));
    }

    [Fact]
    public void Only_the_other_user_answers_a_proposal()
    {
        Assert.Equal(DomainErrorCodes.NotParticipant, CodeOf(() => Proposed().Accept(Ali, Now)));
        Assert.Equal(DomainErrorCodes.NotParticipant, CodeOf(() => Proposed().Decline(Ali, Now)));
        Assert.Equal(DomainErrorCodes.NotParticipant, CodeOf(() => Proposed().Accept(Guid.NewGuid(), Now)));

        var declined = Proposed();
        declined.Decline(Ayse, Now);
        Assert.Equal(SwapSessionStatus.Scheduled, Scheduled().Status);
        Assert.Equal(SwapSessionStatus.Declined, declined.Status);
        Assert.Equal(SessionErrors.InvalidStateCode, CodeOf(() => declined.Accept(Ayse, Now)));
    }

    [Fact]
    public void A_proposal_nobody_answered_counts_as_declined_once_its_start_time_passes()
    {
        var session = Proposed();

        Assert.Equal(SwapSessionStatus.Proposed, session.StatusAt(Start.AddSeconds(-1)));
        Assert.Equal(SwapSessionStatus.Declined, session.StatusAt(Start));
        Assert.Equal(SessionErrors.InvalidStateCode, CodeOf(() => session.Accept(Ayse, Start)));

        session.RecordPassedDeadline(Start.AddSeconds(-1));
        Assert.Equal(SwapSessionStatus.Proposed, session.Status);
        session.RecordPassedDeadline(Start.AddHours(1));
        Assert.Equal(SwapSessionStatus.Declined, session.Status);
    }

    [Fact]
    public void Completion_can_be_marked_only_after_the_start_and_only_on_a_scheduled_session()
    {
        var session = Scheduled();

        Assert.Equal(SessionErrors.SessionNotStartedCode, CodeOf(() => session.MarkCompleted(Ali, Start.AddSeconds(-1))));
        Assert.Equal(SessionErrors.InvalidStateCode, CodeOf(() => Proposed().MarkCompleted(Ali, Start.AddHours(1))));

        session.MarkCompleted(Ayse, Start.AddHours(1));

        Assert.Equal(SwapSessionStatus.CompletionPending, session.Status);
        Assert.Equal(Ayse, session.CompletionMarkedByUserId);
        Assert.Equal(Start.AddHours(73), session.CompletionConfirmDeadline);
    }

    [Fact]
    public void The_other_user_confirms_a_completion_mark()
    {
        var session = Scheduled();
        session.MarkCompleted(Ali, Start.AddHours(1));

        Assert.Equal(DomainErrorCodes.NotParticipant, CodeOf(() => session.ConfirmCompleted(Ali, Start.AddHours(2))));
        session.ConfirmCompleted(Ayse, Start.AddHours(2));

        Assert.Equal(SwapSessionStatus.Completed, session.Status);
        Assert.Equal(Start.AddHours(2), session.CompletedAt);
        Assert.False(session.CompletedAutomatically);
        Assert.Null(session.CompletionConfirmDeadline);
        Assert.Equal(SessionErrors.InvalidStateCode, CodeOf(() => session.Cancel(Ayse, "Olmadı aslında", Start.AddHours(3))));
    }

    [Fact]
    public void An_unanswered_completion_mark_stands_after_seventy_two_hours()
    {
        var session = Scheduled();
        session.MarkCompleted(Ali, Start.AddHours(1));
        var deadline = Start.AddHours(73);

        Assert.Equal(SwapSessionStatus.CompletionPending, session.StatusAt(deadline.AddSeconds(-1)));
        Assert.Equal(SwapSessionStatus.Completed, session.StatusAt(deadline));
        Assert.Equal(SessionErrors.InvalidStateCode, CodeOf(() => session.ConfirmCompleted(Ayse, deadline)));
        Assert.Equal(SessionErrors.InvalidStateCode, CodeOf(() => session.Cancel(Ayse, "Katılmadı", deadline)));

        session.RecordPassedDeadline(deadline.AddHours(5));

        Assert.Equal(SwapSessionStatus.Completed, session.Status);
        Assert.Equal(deadline, session.CompletedAt);
        Assert.True(session.CompletedAutomatically);
    }

    [Fact]
    public void An_in_person_meeting_is_reported_afterwards_and_waits_for_the_other_user()
    {
        var session = SwapSession.ReportInPerson(MatchId, Ali, Ayse, Now);

        Assert.Equal(SessionMode.InPerson, session.Mode);
        Assert.Equal(SwapSessionStatus.CompletionPending, session.Status);
        Assert.Null(session.ScheduledAt);
        Assert.Null(session.DurationMinutes);
        Assert.Equal(Ali, session.CompletionMarkedByUserId);
        Assert.Equal(Now.AddHours(72), session.CompletionConfirmDeadline);

        session.ConfirmCompleted(Ayse, Now.AddHours(1));
        Assert.Equal(SwapSessionStatus.Completed, session.Status);
    }

    [Fact]
    public void A_reported_meeting_is_disputed_by_cancelling_it_with_a_reason()
    {
        var session = SwapSession.ReportInPerson(MatchId, Ali, Ayse, Now);

        session.Cancel(Ayse, "  Böyle bir buluşma olmadı.  ", Now.AddHours(1));

        Assert.Equal(SwapSessionStatus.Cancelled, session.Status);
        Assert.Equal((Ayse, "Böyle bir buluşma olmadı."), (session.CancelledByUserId, session.CancelReason));
        Assert.Equal(SwapSessionStatus.Cancelled, session.StatusAt(Now.AddDays(10)));
    }

    [Fact]
    public void Either_user_can_cancel_an_unfinished_session_but_must_say_why()
    {
        var proposed = Proposed();
        var scheduled = Scheduled();

        Assert.Equal(SessionErrors.InvalidSessionCode, CodeOf(() => proposed.Cancel(Ali, "  ok ", Now)));
        Assert.Equal(SessionErrors.InvalidSessionCode, CodeOf(() => proposed.Cancel(Ali, new string('a', 501), Now)));
        Assert.Equal(DomainErrorCodes.NotParticipant, CodeOf(() => proposed.Cancel(Guid.NewGuid(), "Uygun değilim", Now)));

        proposed.Cancel(Ali, "Uygun değilim", Now);
        scheduled.Cancel(Ayse, "Hastalandım", Now.AddHours(1));

        Assert.Equal(SwapSessionStatus.Cancelled, proposed.Status);
        Assert.Equal((SwapSessionStatus.Cancelled, Now.AddHours(1)), (scheduled.Status, scheduled.CancelledAt));
        Assert.Equal(SessionErrors.InvalidStateCode, CodeOf(() => scheduled.Cancel(Ali, "Tekrar iptal", Now.AddHours(2))));
    }

    [Fact]
    public void When_the_match_ends_sessions_still_ahead_are_cancelled_and_the_rest_are_left_alone()
    {
        var proposed = Proposed();
        var scheduled = Scheduled();
        var pending = SwapSession.ReportInPerson(MatchId, Ali, Ayse, Now);
        var declined = Proposed();
        declined.Decline(Ayse, Now);

        foreach (var session in new[] { proposed, scheduled, pending, declined })
        {
            session.CancelBecauseMatchEnded(Now.AddHours(1));
        }

        Assert.Equal(SwapSessionStatus.Cancelled, proposed.Status);
        Assert.Equal(SwapSessionStatus.Cancelled, scheduled.Status);
        Assert.Null(scheduled.CancelledByUserId);
        Assert.Null(scheduled.CancelReason);
        // A meeting that already took place can still be confirmed.
        Assert.Equal(SwapSessionStatus.CompletionPending, pending.Status);
        Assert.Equal(SwapSessionStatus.Declined, declined.Status);
    }
}
