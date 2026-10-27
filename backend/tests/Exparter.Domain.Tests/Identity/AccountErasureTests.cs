using Exparter.Domain.Common;
using Exparter.Domain.Identity;
using Exparter.Domain.Matching;
using Exparter.Domain.Messaging;

namespace Exparter.Domain.Tests.Identity;

public class AccountErasureTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Ali = Guid.NewGuid();
    private static readonly Guid Ayse = Guid.NewGuid();

    private static User NewUser() => User.Register("Ayse@Example.com", "ayse", "hash", ageConfirmed: true, termsAccepted: true, termsVersion: "v1", Now);

    [Fact]
    public void Erasing_leaves_a_row_that_identifies_nobody()
    {
        var user = NewUser();
        user.VerifyEmail(Now);
        user.RequestDeletion(Now);

        user.Erase(Now + User.DeletionGracePeriod);

        Assert.Equal(UserStatus.Deleted, user.Status);
        Assert.EndsWith("@deleted.invalid", user.Email, StringComparison.Ordinal);
        Assert.StartsWith("d.", user.Username, StringComparison.Ordinal);
        Assert.Equal(Username.MaxLength, user.Username.Length);
        Assert.Equal(string.Empty, user.PasswordHash);
        Assert.False(user.IsEmailVerified);
        Assert.False(user.CancelDeletion());
    }

    [Fact]
    public void Two_erased_accounts_do_not_collide_on_their_placeholders()
    {
        var first = NewUser();
        var second = User.Register("can@example.com", "can", "hash", ageConfirmed: true, termsAccepted: true, termsVersion: "v1", Now);
        foreach (var user in new[] { first, second })
        {
            user.RequestDeletion(Now);
            user.Erase(Now + User.DeletionGracePeriod);
        }

        Assert.NotEqual(first.Email, second.Email);
        Assert.NotEqual(first.Username, second.Username);
    }

    [Fact]
    public void An_account_is_not_erased_while_its_deletion_can_still_be_taken_back()
    {
        var active = NewUser();
        var pending = NewUser();
        pending.RequestDeletion(Now);

        Assert.Throws<DomainException>(() => active.Erase(Now.AddYears(1)));
        Assert.Throws<DomainException>(() => pending.Erase(Now + User.DeletionGracePeriod - TimeSpan.FromSeconds(1)));
        Assert.Equal("ayse@example.com", pending.Email);
    }

    [Fact]
    public void A_blocked_address_is_remembered_by_a_hash_that_ignores_case_and_spaces()
    {
        var blocked = BlockedEmail.For("Ayse@Example.com", Now);

        Assert.Equal(BlockedEmail.HashLength, blocked.EmailHash.Length);
        Assert.Equal(blocked.EmailHash, BlockedEmail.HashOf("  ayse@example.COM "));
        Assert.NotEqual(blocked.EmailHash, BlockedEmail.HashOf("can@example.com"));
        Assert.DoesNotContain("ayse", blocked.EmailHash, StringComparison.Ordinal);
        Assert.Equal(Now.AddDays(90), blocked.BlockedUntil);
    }

    [Fact]
    public void A_leaving_user_withdraws_from_a_pending_request_but_not_from_one_already_answered()
    {
        var pending = SwapRequest.Send(Ali, Ayse, Guid.NewGuid(), Guid.NewGuid(), null, null, Now);
        var declined = SwapRequest.Send(Ali, Ayse, Guid.NewGuid(), Guid.NewGuid(), null, null, Now);
        declined.Decline(Ayse, Now);

        pending.CancelBecauseAccountDeleted(Now.AddHours(1));
        declined.CancelBecauseAccountDeleted(Now.AddHours(1));

        Assert.Equal((SwapRequestStatus.Cancelled, Now.AddHours(1)), (pending.Status, pending.RespondedAt!.Value));
        Assert.Equal(SwapRequestStatus.Declined, declined.Status);
    }

    [Fact]
    public void A_leaving_user_ends_their_match_whether_or_not_an_end_request_is_pending()
    {
        var match = SwapRequest.Send(Ali, Ayse, Guid.NewGuid(), Guid.NewGuid(), null, null, Now).Accept(Ayse, Now);
        match.RequestEnd(Ayse, Now);

        match.EndBecauseAccountDeleted(Now.AddDays(1));
        match.EndBecauseAccountDeleted(Now.AddDays(2));

        Assert.Equal((MatchStatus.Ended, MatchEndReason.AccountDeleted), (match.Status, match.EndReason));
        Assert.Equal(Now.AddDays(1), match.EndedAt);
    }

    [Fact]
    public void A_leaving_user_closes_a_conversation_once()
    {
        var conversation = Conversation.Open(Ali, Ayse, Now);

        conversation.CloseBecauseAccountDeleted(Ali, Now.AddDays(1));
        conversation.CloseBecauseAccountDeleted(Ayse, Now.AddDays(2));

        Assert.True(conversation.IsClosed);
        Assert.Equal((Ali, Now.AddDays(1)), (conversation.ClosedByUserId!.Value, conversation.ClosedAt!.Value));
    }
}
