using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Exparter.Application.Matching;
using Exparter.Application.Notifications;
using Exparter.Application.Sessions;
using Exparter.Infrastructure.Persistence;
using Mediator;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Exparter.Api.IntegrationTests.Notifications;

public class NotificationApiTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(10);

    private readonly HttpClient _client = fixture.CreateClient();

    private static async Task<JsonElement> OkAsync(Task<HttpResponseMessage> pending, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var response = await pending;
        Assert.Equal(expected, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
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

    /// <summary>Two users with an active match.</summary>
    private async Task<(TestSession Ali, TestSession Ayse, Guid MatchId)> MatchedPairAsync()
    {
        var ali = await TestSession.RegisterVisibleAsync(_client, fixture);
        var ayse = await TestSession.RegisterVisibleAsync(_client, fixture);
        var request = await SendRequestAsync(ali, ayse);
        var match = await OkAsync(PostAsync(ayse, $"/api/v1/swap-requests/{request.GetProperty("id").GetGuid()}/accept"));
        return (ali, ayse, match.GetProperty("id").GetGuid());
    }

    private static async Task<List<JsonElement>> NotificationsAsync(TestSession user, bool unreadOnly = false) =>
        (await user.GetJsonAsync($"/api/v1/notifications?unreadOnly={unreadOnly}")).GetProperty("items").EnumerateArray().ToList();

    private static async Task<List<string>> TypesAsync(TestSession user) =>
        (await NotificationsAsync(user)).Select(item => item.GetProperty("type").GetString()!).ToList();

    private static async Task<int> UnreadAsync(TestSession user) =>
        (await user.GetJsonAsync("/api/v1/notifications/unread-count")).GetProperty("count").GetInt32();

    private async Task ExecuteSqlAsync(FormattableString sql)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlInterpolatedAsync(sql);
    }

    /// <summary>What the background worker does on every tick.</summary>
    private async Task RunWorkerAsync()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(new ProcessMatchingDeadlinesCommand());
        await sender.Send(new ProcessSessionDeadlinesCommand());
        await sender.Send(new PurgeOldNotificationsCommand());
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

    [Fact]
    public async Task A_request_and_its_answer_notify_the_other_user_who_reads_them()
    {
        var ali = await TestSession.RegisterVisibleAsync(_client, fixture);
        var ayse = await TestSession.RegisterVisibleAsync(_client, fixture);
        Assert.Equal(0, await UnreadAsync(ayse));

        var request = await SendRequestAsync(ali, ayse);

        var received = Assert.Single(await NotificationsAsync(ayse));
        Assert.Equal("SwapRequestReceived", received.GetProperty("type").GetString());
        Assert.Equal(ali.UserId, received.GetProperty("data").GetProperty("actorUserId").GetGuid());
        Assert.Equal(request.GetProperty("id").GetGuid(), received.GetProperty("data").GetProperty("swapRequestId").GetGuid());
        Assert.Equal(JsonValueKind.Null, received.GetProperty("readAt").ValueKind);
        Assert.Equal(1, await UnreadAsync(ayse));
        Assert.Empty(await NotificationsAsync(ali));

        var path = $"/api/v1/notifications/{received.GetProperty("id").GetGuid()}/read";
        await ProblemAssert.IsAsync(await PostAsync(ali, path), HttpStatusCode.NotFound, "not_found");
        Assert.Equal(HttpStatusCode.NoContent, (await PostAsync(ayse, path)).StatusCode);
        Assert.Equal(0, await UnreadAsync(ayse));
        Assert.Empty(await NotificationsAsync(ayse, unreadOnly: true));
        Assert.NotEqual(JsonValueKind.Null, (await NotificationsAsync(ayse))[0].GetProperty("readAt").ValueKind);

        var match = await OkAsync(PostAsync(ayse, $"/api/v1/swap-requests/{request.GetProperty("id").GetGuid()}/accept"));
        var accepted = Assert.Single(await NotificationsAsync(ali));
        Assert.Equal("SwapRequestAccepted", accepted.GetProperty("type").GetString());
        Assert.Equal(match.GetProperty("id").GetGuid(), accepted.GetProperty("data").GetProperty("matchId").GetGuid());
    }

    [Fact]
    public async Task Sessions_reviews_and_ending_a_match_notify_newest_first_and_can_be_read_all_at_once()
    {
        var (ali, ayse, matchId) = await MatchedPairAsync();
        var matchPath = $"/api/v1/matches/{matchId}";

        var proposed = await OkAsync(
            PostAsync(ali, $"{matchPath}/sessions", new { scheduledAt = DateTimeOffset.UtcNow.AddDays(2), durationMinutes = 60 }),
            HttpStatusCode.Created);
        await OkAsync(PostAsync(ayse, $"/api/v1/sessions/{proposed.GetProperty("id").GetGuid()}/cancel", new { reason = "O gün uygun değilim." }));
        var reported = await OkAsync(PostAsync(ali, $"{matchPath}/sessions/in-person"), HttpStatusCode.Created);
        await OkAsync(PostAsync(ayse, $"/api/v1/sessions/{reported.GetProperty("id").GetGuid()}/completion/confirm"));
        await OkAsync(PostAsync(ayse, "/api/v1/reviews", new { revieweeId = ali.UserId, rating = 9 }), HttpStatusCode.Created);
        await OkAsync(PostAsync(ayse, $"{matchPath}/end-request"));
        await OkAsync(PostAsync(ali, $"{matchPath}/end-request/reject"));

        Assert.Equal(
            ["MatchEndRequested", "ReviewReceived", "SessionCompleted", "SessionCancelled", "SwapRequestAccepted"],
            await TypesAsync(ali));
        Assert.Equal(
            ["MatchEndRequestRejected", "SessionCompletionMarked", "SessionProposed", "SwapRequestReceived"],
            await TypesAsync(ayse));
        var cancelled = (await NotificationsAsync(ali))[3].GetProperty("data");
        Assert.Equal("O gün uygun değilim.", cancelled.GetProperty("reason").GetString());
        Assert.Equal(matchId, cancelled.GetProperty("matchId").GetGuid());
        Assert.Equal(JsonValueKind.String, (await NotificationsAsync(ali))[0].GetProperty("data").GetProperty("deadline").ValueKind);

        Assert.Equal(5, await UnreadAsync(ali));
        Assert.Equal(HttpStatusCode.NoContent, (await PostAsync(ali, "/api/v1/notifications/read-all")).StatusCode);
        Assert.Equal(0, await UnreadAsync(ali));
        Assert.Equal(4, await UnreadAsync(ayse));
        var page = await ali.GetJsonAsync("/api/v1/notifications?pageSize=2&page=2");
        Assert.Equal(5, page.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, page.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task A_notification_reaches_an_open_connection_as_it_is_created()
    {
        var ali = await TestSession.RegisterVisibleAsync(_client, fixture);
        var ayse = await TestSession.RegisterVisibleAsync(_client, fixture);
        await using var connection = await ConnectAsync(ayse);
        var received = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<JsonElement>("NotificationReceived", payload => received.TrySetResult(payload));

        var request = await SendRequestAsync(ali, ayse);

        var pushed = await received.Task.WaitAsync(EventTimeout);
        Assert.Equal("SwapRequestReceived", pushed.GetProperty("type").GetString());
        Assert.Equal(request.GetProperty("id").GetGuid(), pushed.GetProperty("data").GetProperty("swapRequestId").GetGuid());
        Assert.Equal((await NotificationsAsync(ayse))[0].GetProperty("id").GetGuid(), pushed.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task The_worker_reminds_of_deadlines_tells_of_what_lapsed_and_removes_old_notifications()
    {
        var (ali, ayse, matchId) = await MatchedPairAsync();
        var matchPath = $"/api/v1/matches/{matchId}";
        var session = await OkAsync(
            PostAsync(ali, $"{matchPath}/sessions", new { scheduledAt = DateTimeOffset.UtcNow.AddDays(5), durationMinutes = 60 }),
            HttpStatusCode.Created);
        var sessionId = session.GetProperty("id").GetGuid();
        await OkAsync(PostAsync(ayse, $"/api/v1/sessions/{sessionId}/accept"));
        await OkAsync(PostAsync(ali, $"{matchPath}/end-request"));
        await RunWorkerAsync();
        Assert.DoesNotContain("SessionStartingSoon", await TypesAsync(ali));
        Assert.DoesNotContain("MatchEndReminder", await TypesAsync(ayse));

        // Time passes: the session is half an hour away and the end request in its last day.
        await ExecuteSqlAsync($"UPDATE sessions.swap_sessions SET scheduled_at = now() + interval '30 minutes' WHERE id = {sessionId}");
        await ExecuteSqlAsync($"UPDATE matching.matches SET end_request_expires_at = now() + interval '5 hours' WHERE id = {matchId}");
        await RunWorkerAsync();
        await RunWorkerAsync();

        Assert.Equal(1, (await TypesAsync(ali)).Count(type => type == "SessionStartingSoon"));
        Assert.Equal(1, (await TypesAsync(ayse)).Count(type => type == "SessionStartingSoon"));
        Assert.Equal(1, (await TypesAsync(ayse)).Count(type => type == "MatchEndReminder"));
        Assert.DoesNotContain("MatchEndReminder", await TypesAsync(ali));

        // More time passes: nobody answered the end request.
        await ExecuteSqlAsync($"UPDATE matching.matches SET end_request_expires_at = now() - interval '1 minute' WHERE id = {matchId}");
        await RunWorkerAsync();
        Assert.Equal("MatchEnded", (await TypesAsync(ali))[0]);
        Assert.Equal("MatchEnded", (await TypesAsync(ayse))[0]);

        // Much later, what happened today is no longer kept.
        var before = (await NotificationsAsync(ali)).Count;
        await ExecuteSqlAsync(
            $"UPDATE notifications.notifications SET created_at = now() - interval '91 days' WHERE user_id = {ali.UserId} AND type = 'MatchEnded'");
        await RunWorkerAsync();
        Assert.Equal(before - 1, (await NotificationsAsync(ali)).Count);
        Assert.NotEmpty(await NotificationsAsync(ayse));
    }

    [Fact]
    public async Task A_device_is_registered_per_token_and_removed_by_its_owner()
    {
        var ali = await TestSession.RegisterAsync(_client);
        var ayse = await TestSession.RegisterAsync(_client);
        var token = $"fcm-{Guid.NewGuid():N}";

        var device = await OkAsync(PostAsync(ali, "/api/v1/devices", new { fcmToken = token, platform = "Android" }));
        var again = await OkAsync(PostAsync(ayse, "/api/v1/devices", new { fcmToken = token, platform = "Android" }));

        Assert.Equal("Android", device.GetProperty("platform").GetString());
        Assert.Equal(device.GetProperty("id").GetGuid(), again.GetProperty("id").GetGuid());
        var path = $"/api/v1/devices/{device.GetProperty("id").GetGuid()}";

        // The installation now belongs to whoever signed in last.
        await ProblemAssert.IsAsync(await ali.SendAsync(HttpMethod.Delete, path), HttpStatusCode.NotFound, "not_found");
        Assert.Equal(HttpStatusCode.NoContent, (await ayse.SendAsync(HttpMethod.Delete, path)).StatusCode);
        await ProblemAssert.IsAsync(await ayse.SendAsync(HttpMethod.Delete, path), HttpStatusCode.NotFound, "not_found");

        await ProblemAssert.IsAsync(
            await PostAsync(ali, "/api/v1/devices", new { fcmToken = "", platform = "Android" }), HttpStatusCode.BadRequest, "validation_failed");
        await ProblemAssert.IsAsync(
            await PostAsync(ali, "/api/v1/devices", new { platform = "Web" }), HttpStatusCode.BadRequest, "validation_failed");
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/v1/notifications")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/v1/notifications/unread-count")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PostAsJsonAsync("/api/v1/devices", new { })).StatusCode);
    }
}
