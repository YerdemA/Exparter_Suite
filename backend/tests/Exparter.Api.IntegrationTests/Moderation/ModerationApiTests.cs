using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace Exparter.Api.IntegrationTests.Moderation;

public class ModerationApiTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private readonly HttpClient _client = fixture.CreateClient();

    private static async Task<JsonElement> OkAsync(Task<HttpResponseMessage> pending, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var response = await pending;
        Assert.Equal(expected, response.StatusCode);
        return expected == HttpStatusCode.NoContent ? default : await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static Task<HttpResponseMessage> PostAsync(TestSession user, string path, object? body = null) =>
        user.SendAsync(HttpMethod.Post, path, body);

    private static async Task<JsonElement> SendRequestAsync(TestSession from, TestSession to)
    {
        var guitar = await from.FindSkillIdAsync("gitar");
        return await OkAsync(
            PostAsync(from, "/api/v1/swap-requests", new { recipientId = to.UserId, offeredSkillId = guitar, requestedSkillId = guitar }),
            HttpStatusCode.Created);
    }

    private async Task<(TestSession Ali, TestSession Ayse, JsonElement Match)> MatchedPairAsync()
    {
        var ali = await TestSession.RegisterVisibleAsync(_client, fixture);
        var ayse = await TestSession.RegisterVisibleAsync(_client, fixture);
        var request = await SendRequestAsync(ali, ayse);
        var match = await OkAsync(PostAsync(ayse, $"/api/v1/swap-requests/{request.GetProperty("id").GetGuid()}/accept"));
        return (ali, ayse, match);
    }

    private async Task<TestSession> AdminAsync()
    {
        var admin = await TestSession.RegisterVisibleAsync(_client, fixture);
        await fixture.MakeAdminAsync(admin.UserId);
        return await admin.SignInAgainAsync();
    }

    private static async Task<List<string>> NotificationTypesAsync(TestSession user) =>
        (await user.GetJsonAsync("/api/v1/notifications")).GetProperty("items").EnumerateArray()
        .Select(item => item.GetProperty("type").GetString()!)
        .ToList();

    [Fact]
    public async Task A_block_closes_what_the_two_share_and_hides_them_from_each_other_without_telling_the_blocked_user()
    {
        var (ali, ayse, match) = await MatchedPairAsync();
        var matchPath = $"/api/v1/matches/{match.GetProperty("id").GetGuid()}";
        var session = await OkAsync(
            PostAsync(ayse, $"{matchPath}/sessions", new { scheduledAt = DateTimeOffset.UtcNow.AddDays(2), durationMinutes = 60 }),
            HttpStatusCode.Created);
        var before = await NotificationTypesAsync(ayse);

        var block = await OkAsync(PostAsync(ali, "/api/v1/blocks", new { userId = ayse.UserId }), HttpStatusCode.Created);
        Assert.Equal(ayse.UserId, block.GetProperty("user").GetProperty("userId").GetGuid());
        await ProblemAssert.IsAsync(await PostAsync(ali, "/api/v1/blocks", new { userId = ayse.UserId }), HttpStatusCode.Conflict, "already_blocked");

        // The blocker sees why the match ended; the blocked user only that it did.
        Assert.Equal("Blocked", (await ali.GetJsonAsync(matchPath)).GetProperty("endReason").GetString());
        var seenByAyse = await ayse.GetJsonAsync(matchPath);
        Assert.Equal("Ended", seenByAyse.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, seenByAyse.GetProperty("endReason").ValueKind);
        Assert.Equal("Cancelled", (await ayse.GetJsonAsync($"/api/v1/sessions/{session.GetProperty("id").GetGuid()}")).GetProperty("status").GetString());
        Assert.True((await ayse.GetJsonAsync($"/api/v1/conversations/{match.GetProperty("conversationId").GetGuid()}")).GetProperty("isClosed").GetBoolean());
        Assert.Equal(before, await NotificationTypesAsync(ayse));

        // Neither finds the other; trying anyway tells only the blocker why.
        await ProblemAssert.IsAsync(await ali.SendAsync(HttpMethod.Get, $"/api/v1/users/{ayse.UserId}"), HttpStatusCode.NotFound, "not_found");
        await ProblemAssert.IsAsync(await ayse.SendAsync(HttpMethod.Get, $"/api/v1/users/{ali.UserId}"), HttpStatusCode.NotFound, "not_found");
        var found = (await ayse.GetJsonAsync($"/api/v1/discovery/users?q={ali.Username}")).GetProperty("items").EnumerateArray();
        Assert.DoesNotContain(found, item => item.GetProperty("user").GetProperty("userId").GetGuid() == ali.UserId);
        var guitar = await ali.FindSkillIdAsync("gitar");
        await ProblemAssert.IsAsync(
            await PostAsync(ayse, "/api/v1/swap-requests", new { recipientId = ali.UserId, offeredSkillId = guitar, requestedSkillId = guitar }),
            HttpStatusCode.NotFound,
            "not_found");
        await ProblemAssert.IsAsync(
            await PostAsync(ali, "/api/v1/swap-requests", new { recipientId = ayse.UserId, offeredSkillId = guitar, requestedSkillId = guitar }),
            HttpStatusCode.Conflict,
            "user_blocked");

        Assert.Equal(1, (await ali.GetJsonAsync("/api/v1/blocks")).GetProperty("totalCount").GetInt32());
        await OkAsync(ali.SendAsync(HttpMethod.Delete, $"/api/v1/blocks/{ayse.UserId}"), HttpStatusCode.NoContent);

        await OkAsync(ayse.SendAsync(HttpMethod.Get, $"/api/v1/users/{ali.UserId}"));
        Assert.Equal("Ended", (await ali.GetJsonAsync(matchPath)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Reports_keep_what_was_said_and_the_reporter_hears_when_one_was_reviewed()
    {
        var (ali, ayse, match) = await MatchedPairAsync();
        var conversationPath = $"/api/v1/conversations/{match.GetProperty("conversationId").GetGuid()}";
        var message = await OkAsync(PostAsync(ayse, $"{conversationPath}/messages", new { body = "Kaba bir mesaj" }), HttpStatusCode.Created);
        var messageId = message.GetProperty("id").GetGuid();
        var stranger = await TestSession.RegisterVisibleAsync(_client, fixture);

        var report = await OkAsync(
            PostAsync(ali, "/api/v1/reports", new { targetType = "Message", targetId = messageId, reason = "Harassment" }),
            HttpStatusCode.Created);
        Assert.Equal("Open", report.GetProperty("status").GetString());
        await ProblemAssert.IsAsync(
            await PostAsync(ali, "/api/v1/reports", new { targetType = "Message", targetId = messageId, reason = "Spam" }),
            HttpStatusCode.Conflict,
            "report_already_open");
        await ProblemAssert.IsAsync(
            await PostAsync(stranger, "/api/v1/reports", new { targetType = "Message", targetId = messageId, reason = "Spam" }),
            HttpStatusCode.NotFound,
            "not_found");
        await ProblemAssert.IsAsync(
            await PostAsync(ayse, "/api/v1/reports", new { targetType = "Message", targetId = messageId, reason = "Spam" }),
            HttpStatusCode.Conflict,
            "report_self");
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await PostAsync(ali, "/api/v1/reports", new { targetType = "User", targetId = ayse.UserId, reason = "Other" })).StatusCode);

        await ProblemAssert.IsAsync(await ali.SendAsync(HttpMethod.Get, "/api/v1/admin/reports"), HttpStatusCode.Forbidden, "admin_required");
        var admin = await AdminAsync();
        var open = (await admin.GetJsonAsync("/api/v1/admin/reports?status=Open&pageSize=50")).GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == report.GetProperty("id").GetGuid());
        Assert.Equal("Kaba bir mesaj", open.GetProperty("targetContent").GetString());
        Assert.Equal(ayse.UserId, open.GetProperty("targetUserId").GetGuid());
        Assert.Equal(ali.UserId, open.GetProperty("reporterId").GetGuid());

        var closed = await OkAsync(PostAsync(admin, $"/api/v1/admin/reports/{report.GetProperty("id").GetGuid()}/resolution", new { status = "Resolved" }));
        Assert.Equal(admin.UserId, closed.GetProperty("resolvedByUserId").GetGuid());
        await ProblemAssert.IsAsync(
            await PostAsync(admin, $"/api/v1/admin/reports/{report.GetProperty("id").GetGuid()}/resolution", new { status = "Dismissed" }),
            HttpStatusCode.Conflict,
            "report_already_closed");
        Assert.Equal("ReportReviewed", (await NotificationTypesAsync(ali))[0]);
        Assert.DoesNotContain("ReportReviewed", await NotificationTypesAsync(ayse));
    }

    [Fact]
    public async Task A_suspended_account_is_disconnected_cannot_sign_in_and_is_hidden_until_restored()
    {
        var admin = await AdminAsync();
        var (ali, ayse, _) = await MatchedPairAsync();
        var connection = new HubConnectionBuilder()
            .WithUrl(
                new Uri(fixture.Server.BaseAddress, "hubs/realtime"),
                options =>
                {
                    options.HttpMessageHandlerFactory = _ => fixture.Server.CreateHandler();
                    options.Transports = HttpTransportType.LongPolling;
                    options.AccessTokenProvider = () => Task.FromResult<string?>(ayse.AccessToken);
                })
            .Build();
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.Closed += _ =>
        {
            closed.TrySetResult();
            return Task.CompletedTask;
        };
        await connection.StartAsync();

        await OkAsync(PostAsync(admin, $"/api/v1/admin/users/{ayse.UserId}/suspension"), HttpStatusCode.NoContent);

        await closed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await ProblemAssert.IsAsync(
            await _client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = ayse.Username, password = TestSession.Password }),
            HttpStatusCode.Forbidden,
            "account_suspended");
        await ProblemAssert.IsAsync(await ali.SendAsync(HttpMethod.Get, $"/api/v1/users/{ayse.UserId}"), HttpStatusCode.NotFound, "not_found");
        await ProblemAssert.IsAsync(await PostAsync(admin, $"/api/v1/admin/users/{ayse.UserId}/suspension"), HttpStatusCode.Conflict, "invalid_state");
        await ProblemAssert.IsAsync(await PostAsync(admin, $"/api/v1/admin/users/{admin.UserId}/suspension"), HttpStatusCode.Conflict, "invalid_state");
        await ProblemAssert.IsAsync(await PostAsync(ali, $"/api/v1/admin/users/{ayse.UserId}/suspension"), HttpStatusCode.Forbidden, "admin_required");

        await OkAsync(admin.SendAsync(HttpMethod.Delete, $"/api/v1/admin/users/{ayse.UserId}/suspension"), HttpStatusCode.NoContent);

        await ayse.SignInAgainAsync();
        await OkAsync(ali.SendAsync(HttpMethod.Get, $"/api/v1/users/{ayse.UserId}"));
        await connection.DisposeAsync();
    }

    [Fact]
    public async Task Administrators_read_the_reasons_sessions_were_cancelled_for()
    {
        var (ali, ayse, match) = await MatchedPairAsync();
        var session = await OkAsync(
            PostAsync(ali, $"/api/v1/matches/{match.GetProperty("id").GetGuid()}/sessions", new { scheduledAt = DateTimeOffset.UtcNow.AddDays(2), durationMinutes = 60 }),
            HttpStatusCode.Created);
        await OkAsync(PostAsync(ali, $"/api/v1/sessions/{session.GetProperty("id").GetGuid()}/cancel", new { reason = "Hastalandım, kusura bakmayın." }));
        var admin = await AdminAsync();

        var cancellation = (await admin.GetJsonAsync("/api/v1/admin/session-cancellations?pageSize=50")).GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("sessionId").GetGuid() == session.GetProperty("id").GetGuid());

        Assert.Equal("Hastalandım, kusura bakmayın.", cancellation.GetProperty("reason").GetString());
        Assert.Equal(ali.UserId, cancellation.GetProperty("cancelledByUserId").GetGuid());
        Assert.Equal(ayse.UserId, cancellation.GetProperty("otherUserId").GetGuid());
    }
}
