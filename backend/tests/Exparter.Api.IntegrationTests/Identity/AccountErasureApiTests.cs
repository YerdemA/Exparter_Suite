using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Exparter.Application.Identity;
using Exparter.Infrastructure.Persistence;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Exparter.Api.IntegrationTests.Identity;

public class AccountErasureApiTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
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

    private static async Task RequestDeletionAsync(TestSession user) =>
        await OkAsync(PostAsync(user, "/api/v1/me/deletion-request", new { password = TestSession.Password }), HttpStatusCode.Accepted);

    private Task<HttpResponseMessage> RegisterAsync(string email, string username) =>
        _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email, username, password = TestSession.Password, ageConfirmed = true, termsAccepted = true });

    private async Task ExecuteSqlAsync(FormattableString sql)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlInterpolatedAsync(sql);
    }

    private async Task<int> ScalarAsync(FormattableString sql)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.SqlQuery<int>(sql).SingleAsync();
    }

    /// <summary>What the background worker does about deleted accounts on every tick.</summary>
    private async Task<int> EraseDueAccountsAsync()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new EraseDeletedAccountsCommand());
    }

    [Fact]
    public async Task Asking_to_delete_an_account_closes_what_the_user_shares_with_others()
    {
        var leaving = await TestSession.RegisterVisibleAsync(_client, fixture);
        var partner = await TestSession.RegisterVisibleAsync(_client, fixture);
        var asked = await TestSession.RegisterVisibleAsync(_client, fixture);
        var asking = await TestSession.RegisterVisibleAsync(_client, fixture);
        var request = await SendRequestAsync(leaving, partner);
        var match = await OkAsync(PostAsync(partner, $"/api/v1/swap-requests/{request.GetProperty("id").GetGuid()}/accept"));
        var matchPath = $"/api/v1/matches/{match.GetProperty("id").GetGuid()}";
        var session = await OkAsync(
            PostAsync(leaving, $"{matchPath}/sessions", new { scheduledAt = DateTimeOffset.UtcNow.AddDays(2), durationMinutes = 60 }),
            HttpStatusCode.Created);
        var sent = await SendRequestAsync(leaving, asked);
        var received = await SendRequestAsync(asking, leaving);

        await RequestDeletionAsync(leaving);

        var ended = await partner.GetJsonAsync(matchPath);
        Assert.Equal("Ended", ended.GetProperty("status").GetString());
        Assert.Equal("AccountDeleted", ended.GetProperty("endReason").GetString());
        Assert.Equal(
            "Cancelled",
            (await partner.GetJsonAsync($"/api/v1/sessions/{session.GetProperty("id").GetGuid()}")).GetProperty("status").GetString());
        Assert.True(
            (await partner.GetJsonAsync($"/api/v1/conversations/{match.GetProperty("conversationId").GetGuid()}")).GetProperty("isClosed").GetBoolean());
        Assert.Equal(
            "Cancelled",
            (await asked.GetJsonAsync($"/api/v1/swap-requests/{sent.GetProperty("id").GetGuid()}")).GetProperty("status").GetString());
        Assert.Equal(
            "Cancelled",
            (await asking.GetJsonAsync($"/api/v1/swap-requests/{received.GetProperty("id").GetGuid()}")).GetProperty("status").GetString());

        async Task<string> NewestAsync(TestSession user) =>
            (await user.GetJsonAsync("/api/v1/notifications")).GetProperty("items")[0].GetProperty("type").GetString()!;
        Assert.Equal("MatchEnded", await NewestAsync(partner));
        Assert.Equal("SwapRequestCancelled", await NewestAsync(asked));
        Assert.Equal("SwapRequestExpired", await NewestAsync(asking));

        // Signing in again restores the account, but not what was closed.
        var restored = await leaving.SignInAgainAsync();
        Assert.Equal("Ended", (await restored.GetJsonAsync(matchPath)).GetProperty("status").GetString());
        Assert.Equal(0, (await restored.GetJsonAsync("/api/v1/swap-requests?direction=Outgoing&status=Pending")).GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task After_thirty_days_the_account_is_erased_and_what_it_wrote_stays_under_no_name()
    {
        var leaving = await TestSession.RegisterVisibleAsync(_client, fixture);
        var partner = await TestSession.RegisterVisibleAsync(_client, fixture);
        var request = await SendRequestAsync(leaving, partner);
        var match = await OkAsync(PostAsync(partner, $"/api/v1/swap-requests/{request.GetProperty("id").GetGuid()}/accept"));
        var matchPath = $"/api/v1/matches/{match.GetProperty("id").GetGuid()}";
        var conversationPath = $"/api/v1/conversations/{match.GetProperty("conversationId").GetGuid()}";
        await OkAsync(PostAsync(leaving, $"{conversationPath}/messages", new { body = "Merhaba!" }), HttpStatusCode.Created);
        var meeting = await OkAsync(PostAsync(leaving, $"{matchPath}/sessions/in-person"), HttpStatusCode.Created);
        await OkAsync(PostAsync(partner, $"/api/v1/sessions/{meeting.GetProperty("id").GetGuid()}/completion/confirm"));
        await OkAsync(PostAsync(leaving, "/api/v1/reviews", new { revieweeId = partner.UserId, rating = 8, comment = "İyiydi." }), HttpStatusCode.Created);
        await OkAsync(PostAsync(partner, "/api/v1/reviews", new { revieweeId = leaving.UserId, rating = 9 }), HttpStatusCode.Created);
        await OkAsync(PostAsync(leaving, "/api/v1/devices", new { fcmToken = $"fcm-{Guid.NewGuid():N}", platform = "Android" }));

        await RequestDeletionAsync(leaving);
        Assert.Equal(0, await EraseDueAccountsAsync());

        await ExecuteSqlAsync($"UPDATE identity.users SET deletion_requested_at = now() - interval '31 days' WHERE id = {leaving.UserId}");
        Assert.True(await EraseDueAccountsAsync() >= 1);

        // Nothing that describes the user is left.
        Assert.Equal(1, await ScalarAsync($"SELECT count(*)::int AS \"Value\" FROM identity.users WHERE id = {leaving.UserId} AND status = 'Deleted' AND email LIKE '%@deleted.invalid' AND password_hash = ''"));
        Assert.Equal(0, await ScalarAsync($"SELECT count(*)::int AS \"Value\" FROM profiles.profiles WHERE user_id = {leaving.UserId}"));
        Assert.Equal(0, await ScalarAsync($"SELECT count(*)::int AS \"Value\" FROM skills.user_skills WHERE user_id = {leaving.UserId}"));
        Assert.Equal(0, await ScalarAsync($"SELECT count(*)::int AS \"Value\" FROM notifications.notifications WHERE user_id = {leaving.UserId}"));
        Assert.Equal(0, await ScalarAsync($"SELECT count(*)::int AS \"Value\" FROM notifications.devices WHERE user_id = {leaving.UserId}"));
        Assert.Equal(0, await ScalarAsync($"SELECT count(*)::int AS \"Value\" FROM identity.refresh_tokens WHERE user_id = {leaving.UserId}"));
        Assert.Equal(0, await ScalarAsync($"SELECT count(*)::int AS \"Value\" FROM reviews.reviews WHERE reviewee_id = {leaving.UserId}"));

        // What the user wrote stays, under no name.
        var review = Assert.Single((await partner.GetJsonAsync($"/api/v1/users/{partner.UserId}/reviews")).GetProperty("items").EnumerateArray());
        Assert.True(review.GetProperty("reviewer").GetProperty("isDeleted").GetBoolean());
        Assert.Equal(JsonValueKind.Null, review.GetProperty("reviewer").GetProperty("username").ValueKind);
        var conversation = await partner.GetJsonAsync(conversationPath);
        Assert.True(conversation.GetProperty("otherUser").GetProperty("isDeleted").GetBoolean());
        Assert.Equal("Merhaba!", (await partner.GetJsonAsync($"{conversationPath}/messages")).GetProperty("items")[0].GetProperty("body").GetString());
        await ProblemAssert.IsAsync(
            await partner.SendAsync(HttpMethod.Get, $"/api/v1/users/{leaving.UserId}"), HttpStatusCode.NotFound, "not_found");

        // The address waits three months; the name is free at once.
        await ProblemAssert.IsAsync(
            await _client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = leaving.Email, password = TestSession.Password }),
            HttpStatusCode.Unauthorized,
            "invalid_credentials");
        await ProblemAssert.IsAsync(await RegisterAsync(leaving.Email.ToUpperInvariant(), $"x{Guid.NewGuid():N}"[..20]), HttpStatusCode.Conflict, "email_recently_deleted");
        Assert.Equal(HttpStatusCode.Created, (await RegisterAsync($"{Guid.NewGuid():N}@example.com", leaving.Username)).StatusCode);

        await ExecuteSqlAsync($"UPDATE identity.blocked_emails SET blocked_until = now() - interval '1 minute'");
        Assert.Equal(HttpStatusCode.Created, (await RegisterAsync(leaving.Email, $"y{Guid.NewGuid():N}"[..20])).StatusCode);
    }
}
