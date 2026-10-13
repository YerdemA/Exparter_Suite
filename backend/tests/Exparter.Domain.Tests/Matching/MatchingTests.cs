using Exparter.Domain.Common;
using Exparter.Domain.Matching;

namespace Exparter.Domain.Tests.Matching;

public class SwapRequestTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Requester = Guid.NewGuid();
    private static readonly Guid Recipient = Guid.NewGuid();
    private static readonly Guid Offered = Guid.NewGuid();
    private static readonly Guid Requested = Guid.NewGuid();

    private static SwapRequest Send(string? message = null, SwapRequest? lastRefused = null, DateTimeOffset? at = null) =>
        SwapRequest.Send(Requester, Recipient, Offered, Requested, message, lastRefused, at ?? Now);

    private static string CodeOf(Action action) => Assert.Throws<DomainException>(action).Code;

    [Fact]
    public void Send_creates_a_pending_request_that_expires_in_three_days()
    {
        var request = Send("  Merhaba  ");

        Assert.Equal(SwapRequestStatus.Pending, request.Status);
        Assert.Equal(Now.AddDays(3), request.ExpiresAt);
        Assert.Equal("Merhaba", request.Message);
        Assert.Null(request.RespondedAt);
        Assert.True(request.IsParticipant(Requester));
        Assert.True(request.IsParticipant(Recipient));
        Assert.False(request.IsParticipant(Guid.NewGuid()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Send_stores_no_message_for_blank_text(string? message) => Assert.Null(Send(message).Message);

    [Fact]
    public void Send_to_oneself_is_rejected() =>
        Assert.Equal(
            MatchingErrors.SwapRequestToSelfCode,
            CodeOf(() => SwapRequest.Send(Requester, Requester, Offered, Requested, null, null, Now)));

    [Fact]
    public void Accept_by_the_recipient_creates_the_match()
    {
        var request = Send();

        var match = request.Accept(Recipient, Now.AddHours(1));

        Assert.Equal(SwapRequestStatus.Accepted, request.Status);
        Assert.Equal(Now.AddHours(1), request.RespondedAt);
        Assert.Equal(request.Id, match.SwapRequestId);
        Assert.Equal((Requester, Recipient), (match.UserAId, match.UserBId));
        Assert.Equal((Offered, Requested), (match.SkillTaughtByAId, match.SkillTaughtByBId));
        Assert.Equal(MatchStatus.Active, match.Status);
    }

    [Fact]
    public void Only_the_recipient_answers_and_only_the_requester_withdraws()
    {
        Assert.Equal(MatchingErrors.NotParticipantCode, CodeOf(() => Send().Accept(Requester, Now)));
        Assert.Equal(MatchingErrors.NotParticipantCode, CodeOf(() => Send().Decline(Requester, Now)));
        Assert.Equal(MatchingErrors.NotParticipantCode, CodeOf(() => Send().Cancel(Recipient, Now)));
    }

    [Fact]
    public void A_request_can_be_closed_only_once()
    {
        var declined = Send();
        declined.Decline(Recipient, Now);
        var cancelled = Send();
        cancelled.Cancel(Requester, Now);

        Assert.Equal(SwapRequestStatus.Declined, declined.Status);
        Assert.Equal(SwapRequestStatus.Cancelled, cancelled.Status);
        Assert.Equal(MatchingErrors.InvalidStateCode, CodeOf(() => declined.Accept(Recipient, Now)));
        Assert.Equal(MatchingErrors.InvalidStateCode, CodeOf(() => cancelled.Decline(Recipient, Now)));
        Assert.Equal(MatchingErrors.InvalidStateCode, CodeOf(() => declined.Cancel(Requester, Now)));
    }

    [Fact]
    public void A_request_past_its_deadline_counts_as_expired_before_that_is_recorded()
    {
        var request = Send();
        var deadline = request.ExpiresAt;

        Assert.Equal(SwapRequestStatus.Pending, request.StatusAt(deadline.AddSeconds(-1)));
        Assert.Equal(SwapRequestStatus.Expired, request.StatusAt(deadline));
        Assert.Equal(MatchingErrors.InvalidStateCode, CodeOf(() => request.Accept(Recipient, deadline)));
        Assert.Equal(SwapRequestStatus.Pending, request.Status);
    }

    [Fact]
    public void Expiry_is_recorded_only_once_the_deadline_has_passed()
    {
        var request = Send();

        request.ExpireIfOverdue(request.ExpiresAt.AddSeconds(-1));
        Assert.Equal(SwapRequestStatus.Pending, request.Status);

        request.ExpireIfOverdue(request.ExpiresAt.AddHours(5));
        Assert.Equal(SwapRequestStatus.Expired, request.Status);
        Assert.Equal(request.ExpiresAt, request.RespondedAt);
    }

    [Fact]
    public void A_declined_requester_waits_seven_days_before_asking_again()
    {
        var declined = Send();
        declined.Decline(Recipient, Now.AddDays(1));

        Assert.Equal(
            MatchingErrors.SwapRequestCooldownCode,
            CodeOf(() => Send(lastRefused: declined, at: Now.AddDays(8).AddSeconds(-1))));
        Assert.Equal(SwapRequestStatus.Pending, Send(lastRefused: declined, at: Now.AddDays(8)).Status);
    }

    [Fact]
    public void The_wait_after_an_ignored_request_starts_at_its_deadline()
    {
        var ignored = Send();

        Assert.Equal(
            MatchingErrors.SwapRequestCooldownCode,
            CodeOf(() => Send(lastRefused: ignored, at: ignored.ExpiresAt.AddDays(7).AddSeconds(-1))));
        Assert.Equal(SwapRequestStatus.Pending, Send(lastRefused: ignored, at: ignored.ExpiresAt.AddDays(7)).Status);
    }
}

public class MatchTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserA = Guid.NewGuid();
    private static readonly Guid UserB = Guid.NewGuid();

    private static Match NewMatch() =>
        SwapRequest.Send(UserA, UserB, Guid.NewGuid(), Guid.NewGuid(), null, null, Now).Accept(UserB, Now);

    private static string CodeOf(Action action) => Assert.Throws<DomainException>(action).Code;

    [Fact]
    public void Either_user_can_ask_to_end_and_the_other_has_seven_days_to_answer()
    {
        var match = NewMatch();

        match.RequestEnd(UserB, Now);

        Assert.Equal(MatchStatus.EndRequested, match.Status);
        Assert.Equal(UserB, match.EndRequestedByUserId);
        Assert.Equal(Now.AddDays(7), match.EndRequestExpiresAt);
        Assert.Equal(MatchingErrors.InvalidStateCode, CodeOf(() => match.RequestEnd(UserA, Now)));
    }

    [Fact]
    public void Accepting_the_end_request_ends_the_match_and_keeps_who_asked()
    {
        var match = NewMatch();
        match.RequestEnd(UserA, Now);

        match.AcceptEnd(UserB, Now.AddDays(1));

        Assert.Equal(MatchStatus.Ended, match.Status);
        Assert.Equal(MatchEndReason.MutualConsent, match.EndReason);
        Assert.Equal(Now.AddDays(1), match.EndedAt);
        Assert.Equal(UserA, match.EndRequestedByUserId);
    }

    [Fact]
    public void Rejecting_or_withdrawing_the_end_request_makes_the_match_active_again()
    {
        var rejected = NewMatch();
        rejected.RequestEnd(UserA, Now);
        rejected.RejectEnd(UserB);
        var withdrawn = NewMatch();
        withdrawn.RequestEnd(UserA, Now);
        withdrawn.WithdrawEndRequest(UserA);

        foreach (var match in new[] { rejected, withdrawn })
        {
            Assert.Equal(MatchStatus.Active, match.Status);
            Assert.Null(match.EndRequestedByUserId);
            Assert.Null(match.EndRequestExpiresAt);
        }

        // No waiting period before asking again.
        rejected.RequestEnd(UserA, Now);
        Assert.Equal(MatchStatus.EndRequested, rejected.Status);
    }

    [Fact]
    public void Only_the_other_user_answers_an_end_request_and_only_its_author_withdraws_it()
    {
        var match = NewMatch();
        match.RequestEnd(UserA, Now);

        Assert.Equal(MatchingErrors.NotParticipantCode, CodeOf(() => match.AcceptEnd(UserA, Now)));
        Assert.Equal(MatchingErrors.NotParticipantCode, CodeOf(() => match.RejectEnd(UserA)));
        Assert.Equal(MatchingErrors.NotParticipantCode, CodeOf(() => match.WithdrawEndRequest(UserB)));
        Assert.Equal(MatchingErrors.NotParticipantCode, CodeOf(() => match.AcceptEnd(Guid.NewGuid(), Now)));
        Assert.Equal(MatchingErrors.NotParticipantCode, CodeOf(() => NewMatch().RequestEnd(Guid.NewGuid(), Now)));
    }

    [Fact]
    public void Answers_need_a_pending_end_request()
    {
        var match = NewMatch();

        Assert.Equal(MatchingErrors.InvalidStateCode, CodeOf(() => match.AcceptEnd(UserB, Now)));
        Assert.Equal(MatchingErrors.InvalidStateCode, CodeOf(() => match.RejectEnd(UserB)));
        Assert.Equal(MatchingErrors.InvalidStateCode, CodeOf(() => match.WithdrawEndRequest(UserA)));
    }

    [Fact]
    public void An_unanswered_end_request_ends_the_match_at_its_deadline()
    {
        var match = NewMatch();
        match.RequestEnd(UserA, Now);

        match.EndIfEndRequestOverdue(Now.AddDays(7).AddSeconds(-1));
        Assert.Equal(MatchStatus.EndRequested, match.Status);

        match.EndIfEndRequestOverdue(Now.AddDays(9));
        Assert.Equal(MatchStatus.Ended, match.Status);
        Assert.Equal(MatchEndReason.EndRequestExpired, match.EndReason);
        Assert.Equal(Now.AddDays(7), match.EndedAt);
    }

    [Fact]
    public void An_ended_match_cannot_be_reopened()
    {
        var match = NewMatch();
        match.RequestEnd(UserA, Now);
        match.AcceptEnd(UserB, Now);

        Assert.Equal(MatchingErrors.InvalidStateCode, CodeOf(() => match.RequestEnd(UserA, Now)));
        Assert.Equal(MatchingErrors.InvalidStateCode, CodeOf(() => match.RejectEnd(UserB)));
    }
}
