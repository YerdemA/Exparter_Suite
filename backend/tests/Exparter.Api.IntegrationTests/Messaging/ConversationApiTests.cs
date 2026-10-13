using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace Exparter.Api.IntegrationTests.Messaging;

public class ConversationApiTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(10);

    private readonly HttpClient _client = fixture.CreateClient();

    /// <summary>Two users with a match, and the conversation that match opened.</summary>
    private async Task<(TestSession Ali, TestSession Ayse, string MatchPath, string ConversationPath)> MatchedPairAsync()
    {
        var ali = await TestSession.RegisterVisibleAsync(_client, fixture);
        var ayse = await TestSession.RegisterVisibleAsync(_client, fixture);
        var guitar = await ali.FindSkillIdAsync("gitar");
        var request = await OkAsync(
            ali.SendAsync(
                HttpMethod.Post,
                "/api/v1/swap-requests",
                new { recipientId = ayse.UserId, offeredSkillId = guitar, requestedSkillId = guitar }),
            HttpStatusCode.Created);
        var match = await OkAsync(
            ayse.SendAsync(HttpMethod.Post, $"/api/v1/swap-requests/{request.GetProperty("id").GetGuid()}/accept"));
        return (
            ali,
            ayse,
            $"/api/v1/matches/{match.GetProperty("id").GetGuid()}",
            $"/api/v1/conversations/{match.GetProperty("conversationId").GetGuid()}");
    }

    private static async Task<JsonElement> OkAsync(Task<HttpResponseMessage> pending, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var response = await pending;
        Assert.Equal(expected, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static Task<JsonElement> SendMessageAsync(TestSession from, string conversationPath, string body, Guid? clientMessageId = null) =>
        OkAsync(
            from.SendAsync(HttpMethod.Post, $"{conversationPath}/messages", new { body, clientMessageId }),
            HttpStatusCode.Created);

    private async Task EndMatchAsync(TestSession first, TestSession second, string matchPath)
    {
        await OkAsync(first.SendAsync(HttpMethod.Post, $"{matchPath}/end-request"));
        await OkAsync(second.SendAsync(HttpMethod.Post, $"{matchPath}/end-request/accept"));
    }

    private async Task<HubConnection> ConnectAsync(TestSession session)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(
                new Uri(fixture.Server.BaseAddress, "hubs/realtime"),
                options =>
                {
                    // The in-memory test server speaks HTTP only, so no WebSocket.
                    options.HttpMessageHandlerFactory = _ => fixture.Server.CreateHandler();
                    options.Transports = HttpTransportType.LongPolling;
                    options.AccessTokenProvider = () => Task.FromResult<string?>(session.AccessToken);
                })
            .Build();
        await connection.StartAsync();
        return connection;
    }

    private static Task<JsonElement> NextAsync(HubConnection connection, string eventName)
    {
        var received = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<JsonElement>(eventName, payload => received.TrySetResult(payload));
        return received.Task.WaitAsync(EventTimeout);
    }

    [Fact]
    public async Task Accepting_a_request_opens_one_conversation_that_both_users_see()
    {
        var (ali, ayse, matchPath, conversationPath) = await MatchedPairAsync();

        var forAli = await ali.GetJsonAsync(conversationPath);
        var listForAyse = await ayse.GetJsonAsync("/api/v1/conversations");

        Assert.Equal(ayse.Username, forAli.GetProperty("otherUser").GetProperty("username").GetString());
        Assert.False(forAli.GetProperty("isClosed").GetBoolean());
        Assert.True(forAli.GetProperty("hasActiveMatch").GetBoolean());
        Assert.Equal(0, forAli.GetProperty("unreadCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, forAli.GetProperty("lastMessage").ValueKind);
        var listed = Assert.Single(listForAyse.GetProperty("items").EnumerateArray());
        Assert.Equal(forAli.GetProperty("id").GetGuid(), listed.GetProperty("id").GetGuid());
        Assert.Equal(ali.Username, listed.GetProperty("otherUser").GetProperty("username").GetString());
        Assert.Equal(
            forAli.GetProperty("id").GetGuid(),
            (await ayse.GetJsonAsync(matchPath)).GetProperty("conversationId").GetGuid());
    }

    [Fact]
    public async Task Messages_are_exchanged_counted_as_unread_and_marked_read()
    {
        var (ali, ayse, _, conversationPath) = await MatchedPairAsync();

        var first = await SendMessageAsync(ali, conversationPath, "  Merhaba!  ");
        await SendMessageAsync(ali, conversationPath, "Ne zaman başlayalım?");

        Assert.Equal("Merhaba!", first.GetProperty("body").GetString());
        Assert.Equal(ali.UserId, first.GetProperty("senderId").GetGuid());
        var forAyse = await ayse.GetJsonAsync(conversationPath);
        Assert.Equal(2, forAyse.GetProperty("unreadCount").GetInt32());
        Assert.Equal("Ne zaman başlayalım?", forAyse.GetProperty("lastMessage").GetProperty("body").GetString());
        Assert.Equal(0, (await ali.GetJsonAsync(conversationPath)).GetProperty("unreadCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, (await ali.GetJsonAsync(conversationPath)).GetProperty("otherUserLastReadAt").ValueKind);

        Assert.Equal(HttpStatusCode.NoContent, (await ayse.SendAsync(HttpMethod.Post, $"{conversationPath}/read")).StatusCode);

        Assert.Equal(0, (await ayse.GetJsonAsync(conversationPath)).GetProperty("unreadCount").GetInt32());
        Assert.NotEqual(JsonValueKind.Null, (await ali.GetJsonAsync(conversationPath)).GetProperty("otherUserLastReadAt").ValueKind);

        await SendMessageAsync(ayse, conversationPath, "Yarın olur.");
        var forAli = (await ali.GetJsonAsync("/api/v1/conversations")).GetProperty("items")[0];
        Assert.Equal(1, forAli.GetProperty("unreadCount").GetInt32());
        Assert.Equal("Yarın olur.", forAli.GetProperty("lastMessage").GetProperty("body").GetString());
    }

    [Fact]
    public async Task Messages_are_paged_from_newest_to_oldest()
    {
        var (ali, ayse, _, conversationPath) = await MatchedPairAsync();
        for (var index = 1; index <= 5; index++)
        {
            await SendMessageAsync(index % 2 == 0 ? ali : ayse, conversationPath, $"Mesaj {index}");
        }

        var first = await ali.GetJsonAsync($"{conversationPath}/messages?limit=2");
        var second = await ali.GetJsonAsync($"{conversationPath}/messages?limit=2&cursor={first.GetProperty("nextCursor").GetString()}");
        var third = await ali.GetJsonAsync($"{conversationPath}/messages?limit=2&cursor={second.GetProperty("nextCursor").GetString()}");

        var bodies = new[] { first, second, third }
            .SelectMany(page => page.GetProperty("items").EnumerateArray())
            .Select(message => message.GetProperty("body").GetString());
        Assert.Equal(["Mesaj 5", "Mesaj 4", "Mesaj 3", "Mesaj 2", "Mesaj 1"], bodies);
        Assert.Equal(JsonValueKind.Null, third.GetProperty("nextCursor").ValueKind);
        await ProblemAssert.IsAsync(
            await ali.SendAsync(HttpMethod.Get, $"{conversationPath}/messages?cursor=abc"), HttpStatusCode.BadRequest, "validation_failed");
        await ProblemAssert.IsAsync(
            await ali.SendAsync(HttpMethod.Get, $"{conversationPath}/messages?limit=51"), HttpStatusCode.BadRequest, "validation_failed");
    }

    [Fact]
    public async Task A_retried_send_is_stored_once()
    {
        var (ali, _, _, conversationPath) = await MatchedPairAsync();
        var clientMessageId = Guid.NewGuid();

        var first = await SendMessageAsync(ali, conversationPath, "Merhaba", clientMessageId);
        var second = await SendMessageAsync(ali, conversationPath, "Merhaba", clientMessageId);

        Assert.Equal(first.GetProperty("id").GetGuid(), second.GetProperty("id").GetGuid());
        Assert.Equal(clientMessageId, second.GetProperty("clientMessageId").GetGuid());
        Assert.Single((await ali.GetJsonAsync($"{conversationPath}/messages")).GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Invalid_messages_and_strangers_are_rejected()
    {
        var (ali, _, _, conversationPath) = await MatchedPairAsync();
        var stranger = await TestSession.RegisterVisibleAsync(_client, fixture);

        await ProblemAssert.IsAsync(
            await ali.SendAsync(HttpMethod.Post, $"{conversationPath}/messages", new { body = "" }), HttpStatusCode.BadRequest, "validation_failed");
        await ProblemAssert.IsAsync(
            await ali.SendAsync(HttpMethod.Post, $"{conversationPath}/messages", new { body = new string('a', 2001) }),
            HttpStatusCode.BadRequest,
            "validation_failed");
        await ProblemAssert.IsAsync(
            await ali.SendAsync(HttpMethod.Post, $"{conversationPath}/messages", new { body = "   " }), HttpStatusCode.BadRequest, "validation_failed");
        await ProblemAssert.IsAsync(await stranger.SendAsync(HttpMethod.Get, conversationPath), HttpStatusCode.NotFound, "not_found");
        await ProblemAssert.IsAsync(await stranger.SendAsync(HttpMethod.Get, $"{conversationPath}/messages"), HttpStatusCode.NotFound, "not_found");
        await ProblemAssert.IsAsync(
            await stranger.SendAsync(HttpMethod.Post, $"{conversationPath}/messages", new { body = "Merhaba" }), HttpStatusCode.NotFound, "not_found");
        await ProblemAssert.IsAsync(await stranger.SendAsync(HttpMethod.Post, $"{conversationPath}/read"), HttpStatusCode.NotFound, "not_found");
        await ProblemAssert.IsAsync(await stranger.SendAsync(HttpMethod.Post, $"{conversationPath}/close"), HttpStatusCode.NotFound, "not_found");
        Assert.Equal(0, (await stranger.GetJsonAsync("/api/v1/conversations")).GetProperty("totalCount").GetInt32());
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/v1/conversations")).StatusCode);
    }

    [Fact]
    public async Task A_conversation_outlives_the_match_can_then_be_closed_and_reopens_on_a_new_match()
    {
        var (ali, ayse, matchPath, conversationPath) = await MatchedPairAsync();
        await SendMessageAsync(ali, conversationPath, "Merhaba");

        await ProblemAssert.IsAsync(
            await ali.SendAsync(HttpMethod.Post, $"{conversationPath}/close"), HttpStatusCode.Conflict, "conversation_has_active_match");

        await EndMatchAsync(ali, ayse, matchPath);
        Assert.False((await ali.GetJsonAsync(conversationPath)).GetProperty("hasActiveMatch").GetBoolean());
        // The match is over, but the two can keep talking.
        await SendMessageAsync(ayse, conversationPath, "Görüşmek üzere");

        var closed = await OkAsync(ayse.SendAsync(HttpMethod.Post, $"{conversationPath}/close"));
        Assert.True(closed.GetProperty("isClosed").GetBoolean());
        Assert.Equal(ayse.UserId, closed.GetProperty("closedByUserId").GetGuid());
        await ProblemAssert.IsAsync(
            await ali.SendAsync(HttpMethod.Post, $"{conversationPath}/messages", new { body = "Orada mısınız?" }),
            HttpStatusCode.Conflict,
            "conversation_closed");
        Assert.Equal(2, (await ali.GetJsonAsync($"{conversationPath}/messages")).GetProperty("items").GetArrayLength());

        // Matching again continues the same conversation, history included.
        var guitar = await ali.FindSkillIdAsync("gitar");
        var request = await OkAsync(
            ayse.SendAsync(
                HttpMethod.Post,
                "/api/v1/swap-requests",
                new { recipientId = ali.UserId, offeredSkillId = guitar, requestedSkillId = guitar }),
            HttpStatusCode.Created);
        var rematch = await OkAsync(
            ali.SendAsync(HttpMethod.Post, $"/api/v1/swap-requests/{request.GetProperty("id").GetGuid()}/accept"));

        Assert.EndsWith(rematch.GetProperty("conversationId").GetGuid().ToString(), conversationPath);
        var reopened = await ali.GetJsonAsync(conversationPath);
        Assert.False(reopened.GetProperty("isClosed").GetBoolean());
        Assert.True(reopened.GetProperty("hasActiveMatch").GetBoolean());
        await SendMessageAsync(ali, conversationPath, "Yeniden merhaba");
        Assert.Equal(3, (await ayse.GetJsonAsync($"{conversationPath}/messages")).GetProperty("items").GetArrayLength());
        Assert.Equal(1, (await ali.GetJsonAsync("/api/v1/conversations")).GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task The_other_user_receives_messages_and_read_receipts_in_real_time()
    {
        var (ali, ayse, _, conversationPath) = await MatchedPairAsync();
        await using var ayseConnection = await ConnectAsync(ayse);
        await using var aliConnection = await ConnectAsync(ali);
        var incoming = NextAsync(ayseConnection, "MessageReceived");
        var echo = NextAsync(aliConnection, "MessageReceived");
        var clientMessageId = Guid.NewGuid();

        var sent = await SendMessageAsync(ali, conversationPath, "Merhaba", clientMessageId);

        var received = await incoming;
        Assert.Equal(sent.GetProperty("id").GetGuid(), received.GetProperty("id").GetGuid());
        Assert.Equal("Merhaba", received.GetProperty("body").GetString());
        Assert.Equal(ali.UserId, received.GetProperty("senderId").GetGuid());
        // The sender's own connections hear it too, with the id they chose.
        Assert.Equal(clientMessageId, (await echo).GetProperty("clientMessageId").GetGuid());

        var read = NextAsync(aliConnection, "ConversationRead");
        await ayse.SendAsync(HttpMethod.Post, $"{conversationPath}/read");

        var receipt = await read;
        Assert.Equal(ayse.UserId, receipt.GetProperty("userId").GetGuid());
        Assert.EndsWith(receipt.GetProperty("conversationId").GetGuid().ToString(), conversationPath);
    }

    [Fact]
    public async Task Closing_tells_each_user_about_the_conversation_as_they_see_it_and_strangers_hear_nothing()
    {
        var (ali, ayse, matchPath, conversationPath) = await MatchedPairAsync();
        var stranger = await TestSession.RegisterVisibleAsync(_client, fixture);
        await EndMatchAsync(ali, ayse, matchPath);
        await using var aliConnection = await ConnectAsync(ali);
        await using var strangerConnection = await ConnectAsync(stranger);
        var update = NextAsync(aliConnection, "ConversationUpdated");
        var leaked = NextAsync(strangerConnection, "ConversationUpdated");

        await OkAsync(ayse.SendAsync(HttpMethod.Post, $"{conversationPath}/close"));

        var updated = await update;
        Assert.True(updated.GetProperty("isClosed").GetBoolean());
        Assert.Equal(ayse.Username, updated.GetProperty("otherUser").GetProperty("username").GetString());
        await Assert.ThrowsAsync<TimeoutException>(() => leaked.WaitAsync(TimeSpan.FromMilliseconds(500)));
    }

    [Fact]
    public async Task The_hub_accepts_the_token_in_the_query_string_and_refuses_anonymous_connections()
    {
        var session = await TestSession.RegisterAsync(_client);

        var anonymous = await _client.PostAsync("/hubs/realtime/negotiate?negotiateVersion=1", null);
        var withToken = await _client.PostAsync(
            $"/hubs/realtime/negotiate?negotiateVersion=1&access_token={session.AccessToken}",
            null);
        var tokenElsewhere = await _client.GetAsync($"/api/v1/me?access_token={session.AccessToken}");

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.OK, withToken.StatusCode);
        // Only the hub reads the query string; everywhere else the token must be in the header.
        Assert.Equal(HttpStatusCode.Unauthorized, tokenElsewhere.StatusCode);
    }
}
