using Exparter.Domain.Common;
using Exparter.Domain.Messaging;

namespace Exparter.Domain.Tests.Messaging;

public class ConversationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Ali = Guid.Parse("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid Ayse = Guid.Parse("00000000-0000-0000-0000-00000000000b");

    private static string CodeOf(Action action) => Assert.Throws<DomainException>(action).Code;

    [Fact]
    public void A_pair_maps_to_the_same_conversation_whichever_user_comes_first()
    {
        var first = Conversation.Open(Ali, Ayse, Now);
        var second = Conversation.Open(Ayse, Ali, Now);

        Assert.Equal((first.UserLowId, first.UserHighId), (second.UserLowId, second.UserHighId));
        Assert.Equal(Conversation.Order(Ali, Ayse), Conversation.Order(Ayse, Ali));
        Assert.True(first.IsParticipant(Ali));
        Assert.True(first.IsParticipant(Ayse));
        Assert.False(first.IsParticipant(Guid.NewGuid()));
        Assert.Equal(Ayse, first.OtherThan(Ali));
        Assert.Equal(Ali, first.OtherThan(Ayse));
    }

    [Fact]
    public void Posting_stores_trimmed_text_and_counts_as_the_sender_having_read()
    {
        var conversation = Conversation.Open(Ali, Ayse, Now);
        var clientId = Guid.NewGuid();

        var message = conversation.Post(Ali, "  Merhaba  ", clientId, Now.AddMinutes(5));

        Assert.Equal("Merhaba", message.Body);
        Assert.Equal((conversation.Id, Ali, clientId), (message.ConversationId, message.SenderId, message.ClientMessageId));
        Assert.Equal(Now.AddMinutes(5), message.SentAt);
        Assert.Equal(message.Id, conversation.LastMessageId);
        Assert.Equal(Now.AddMinutes(5), conversation.LastActivityAt);
        Assert.Equal(Now.AddMinutes(5), conversation.LastReadAtOf(Ali));
        Assert.Null(conversation.LastReadAtOf(Ayse));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_message_needs_text(string body) =>
        Assert.Equal(MessagingErrors.InvalidMessageCode, CodeOf(() => Conversation.Open(Ali, Ayse, Now).Post(Ali, body, null, Now)));

    [Fact]
    public void A_message_is_at_most_two_thousand_characters()
    {
        var conversation = Conversation.Open(Ali, Ayse, Now);

        Assert.Equal(2000, conversation.Post(Ali, new string('a', 2000), null, Now).Body.Length);
        Assert.Equal(MessagingErrors.InvalidMessageCode, CodeOf(() => conversation.Post(Ali, new string('a', 2001), null, Now)));
    }

    [Fact]
    public void Marking_read_records_the_time_for_that_user_only()
    {
        var conversation = Conversation.Open(Ali, Ayse, Now);

        conversation.MarkRead(Ayse, Now.AddHours(1));

        Assert.Equal(Now.AddHours(1), conversation.LastReadAtOf(Ayse));
        Assert.Null(conversation.LastReadAtOf(Ali));
    }

    [Fact]
    public void A_conversation_cannot_be_closed_while_the_pair_has_an_unfinished_match()
    {
        var conversation = Conversation.Open(Ali, Ayse, Now);

        Assert.Equal(
            MessagingErrors.ConversationHasActiveMatchCode,
            CodeOf(() => conversation.Close(Ali, hasActiveMatch: true, Now)));
        Assert.False(conversation.IsClosed);
    }

    [Fact]
    public void A_closed_conversation_is_read_only_and_remembers_who_closed_it()
    {
        var conversation = Conversation.Open(Ali, Ayse, Now);

        conversation.Close(Ayse, hasActiveMatch: false, Now.AddDays(1));
        // Closing again, by either user, changes nothing.
        conversation.Close(Ali, hasActiveMatch: false, Now.AddDays(2));

        Assert.True(conversation.IsClosed);
        Assert.Equal((Now.AddDays(1), Ayse), (conversation.ClosedAt, conversation.ClosedByUserId));
        Assert.Equal(MessagingErrors.ConversationClosedCode, CodeOf(() => conversation.Post(Ali, "Merhaba", null, Now.AddDays(3))));
    }

    [Fact]
    public void Reopening_lets_the_pair_write_again_and_moves_the_conversation_up()
    {
        var conversation = Conversation.Open(Ali, Ayse, Now);
        conversation.Close(Ayse, hasActiveMatch: false, Now.AddDays(1));

        conversation.Reopen(Now.AddDays(5));

        Assert.False(conversation.IsClosed);
        Assert.Null(conversation.ClosedByUserId);
        Assert.Equal(Now.AddDays(5), conversation.LastActivityAt);
        Assert.Equal("Merhaba", conversation.Post(Ali, "Merhaba", null, Now.AddDays(6)).Body);
    }
}
