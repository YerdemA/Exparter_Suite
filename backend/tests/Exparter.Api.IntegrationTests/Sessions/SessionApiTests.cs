using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Exparter.Application.Sessions;
using Exparter.Infrastructure.Persistence;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Exparter.Api.IntegrationTests.Sessions;

public class SessionApiTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private readonly HttpClient _client = fixture.CreateClient();

    /// <summary>Two users with an active match.</summary>
    private async Task<(TestSession Ali, TestSession Ayse, string MatchPath)> MatchedPairAsync()
    {
        var ali = await TestSession.RegisterVisibleAsync(_client, fixture);
        var ayse = await TestSession.RegisterVisibleAsync(_client, fixture);
        return (ali, ayse, await MatchAsync(ali, ayse));
    }

    /// <returns>The path of the new match.</returns>
    private static async Task<string> MatchAsync(TestSession first, TestSession second)
    {
        var guitar = await first.FindSkillIdAsync("gitar");
        var request = await OkAsync(
            first.SendAsync(
                HttpMethod.Post,
                "/api/v1/swap-requests",
                new { recipientId = second.UserId, offeredSkillId = guitar, requestedSkillId = guitar }),
            HttpStatusCode.Created);
        var match = await OkAsync(
            second.SendAsync(HttpMethod.Post, $"/api/v1/swap-requests/{request.GetProperty("id").GetGuid()}/accept"));
        return $"/api/v1/matches/{match.GetProperty("id").GetGuid()}";
    }

    private static async Task<JsonElement> OkAsync(Task<HttpResponseMessage> pending, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var response = await pending;
        Assert.Equal(expected, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static Task<HttpResponseMessage> ProposeAsync(TestSession user, string matchPath, DateTimeOffset? at = null, int duration = 60, string? note = null) =>
        user.SendAsync(
            HttpMethod.Post,
            $"{matchPath}/sessions",
            new { scheduledAt = at ?? DateTimeOffset.UtcNow.AddDays(2), durationMinutes = duration, note });

    private static string PathOf(JsonElement session) => $"/api/v1/sessions/{session.GetProperty("id").GetGuid()}";

    private static string StatusOf(JsonElement session) => session.GetProperty("status").GetString()!;

    private static Task<HttpResponseMessage> PostAsync(TestSession user, string path, object? body = null) =>
        user.SendAsync(HttpMethod.Post, path, body);

    /// <summary>Moves a stored time into the past, standing in for time going by.</summary>
    private async Task ExecuteSqlAsync(FormattableString sql)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlInterpolatedAsync(sql);
    }

    private async Task EndMatchAsync(TestSession first, TestSession second, string matchPath)
    {
        await OkAsync(PostAsync(first, $"{matchPath}/end-request"));
        await OkAsync(PostAsync(second, $"{matchPath}/end-request/accept"));
    }

    private static async Task<(int Total, int Completed, int Cancelled, int Sessions)> StatsAsync(TestSession viewer, TestSession subject)
    {
        var stats = await viewer.GetJsonAsync($"/api/v1/users/{subject.UserId}/stats");
        return (
            stats.GetProperty("totalMatches").GetInt32(),
            stats.GetProperty("completedMatches").GetInt32(),
            stats.GetProperty("cancelledMatches").GetInt32(),
            stats.GetProperty("completedSessions").GetInt32());
    }

    [Fact]
    public async Task An_online_session_is_proposed_accepted_held_and_completed()
    {
        var (ali, ayse, matchPath) = await MatchedPairAsync();

        var proposed = await OkAsync(ProposeAsync(ali, matchPath, duration: 90, note: "  https://meet.example/abc  "), HttpStatusCode.Created);

        Assert.Equal("Proposed", StatusOf(proposed));
        Assert.Equal("Online", proposed.GetProperty("mode").GetString());
        Assert.Equal(90, proposed.GetProperty("durationMinutes").GetInt32());
        Assert.Equal("https://meet.example/abc", proposed.GetProperty("note").GetString());
        Assert.Equal(ali.UserId, proposed.GetProperty("proposedByUserId").GetGuid());
        var path = PathOf(proposed);

        await ProblemAssert.IsAsync(await PostAsync(ali, $"{path}/accept"), HttpStatusCode.Forbidden, "not_participant");
        Assert.Equal("Scheduled", StatusOf(await OkAsync(PostAsync(ayse, $"{path}/accept"))));
        await ProblemAssert.IsAsync(await PostAsync(ali, $"{path}/completion"), HttpStatusCode.Conflict, "session_not_started");

        await ExecuteSqlAsync(
            $"UPDATE sessions.swap_sessions SET scheduled_at = now() - interval '1 hour' WHERE id = {proposed.GetProperty("id").GetGuid()}");
        var marked = await OkAsync(PostAsync(ali, $"{path}/completion"));
        Assert.Equal("CompletionPending", StatusOf(marked));
        Assert.Equal(
            marked.GetProperty("completionMarkedAt").GetDateTimeOffset().AddHours(72),
            marked.GetProperty("completionConfirmDeadline").GetDateTimeOffset());
        await ProblemAssert.IsAsync(await PostAsync(ali, $"{path}/completion/confirm"), HttpStatusCode.Forbidden, "not_participant");

        var completed = await OkAsync(PostAsync(ayse, $"{path}/completion/confirm"));
        Assert.Equal("Completed", StatusOf(completed));
        Assert.False(completed.GetProperty("completedAutomatically").GetBoolean());
        Assert.Equal("Completed", StatusOf(await ali.GetJsonAsync(path)));
        await ProblemAssert.IsAsync(
            await PostAsync(ali, $"{path}/cancel", new { reason = "Vazgeçtim aslında" }), HttpStatusCode.Conflict, "invalid_state");
    }

    [Fact]
    public async Task An_in_person_meeting_is_reported_without_time_and_confirmed_by_the_other_user()
    {
        var (ali, ayse, matchPath) = await MatchedPairAsync();

        var reported = await OkAsync(PostAsync(ayse, $"{matchPath}/sessions/in-person"), HttpStatusCode.Created);

        Assert.Equal("InPerson", reported.GetProperty("mode").GetString());
        Assert.Equal("CompletionPending", StatusOf(reported));
        Assert.Equal(JsonValueKind.Null, reported.GetProperty("scheduledAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, reported.GetProperty("durationMinutes").ValueKind);
        Assert.Equal(ayse.UserId, reported.GetProperty("completionMarkedByUserId").GetGuid());
        await ProblemAssert.IsAsync(
            await PostAsync(ayse, $"{PathOf(reported)}/completion/confirm"), HttpStatusCode.Forbidden, "not_participant");

        Assert.Equal("Completed", StatusOf(await OkAsync(PostAsync(ali, $"{PathOf(reported)}/completion/confirm"))));
    }

    [Fact]
    public async Task A_pair_can_report_an_in_person_meeting_only_once_unless_it_was_cancelled()
    {
        var (ali, ayse, matchPath) = await MatchedPairAsync();
        var first = await OkAsync(PostAsync(ali, $"{matchPath}/sessions/in-person"), HttpStatusCode.Created);

        await ProblemAssert.IsAsync(
            await PostAsync(ayse, $"{matchPath}/sessions/in-person"), HttpStatusCode.Conflict, "in_person_already_reported");
        await ProblemAssert.IsAsync(
            await PostAsync(ali, $"{matchPath}/sessions/in-person"), HttpStatusCode.Conflict, "in_person_already_reported");

        await OkAsync(PostAsync(ayse, $"{PathOf(first)}/cancel", new { reason = "Böyle bir buluşma olmadı." }));
        var second = await OkAsync(PostAsync(ayse, $"{matchPath}/sessions/in-person"), HttpStatusCode.Created);
        await OkAsync(PostAsync(ali, $"{PathOf(second)}/completion/confirm"));

        await ProblemAssert.IsAsync(
            await PostAsync(ali, $"{matchPath}/sessions/in-person"), HttpStatusCode.Conflict, "in_person_already_reported");

        // The database refuses a second one even if two reports slip past the check together.
        var duplicate = await Record.ExceptionAsync(() => ExecuteSqlAsync(
            $"""
            INSERT INTO sessions.swap_sessions (id, match_id, proposed_by_user_id, other_user_id, mode, status, completed_automatically, created_at)
            VALUES ({Guid.NewGuid()}, {Guid.NewGuid()}, {ayse.UserId}, {ali.UserId}, 'InPerson', 'CompletionPending', false, now())
            """));
        Assert.Contains("ix_swap_sessions_in_person_pair", duplicate?.Message);
    }

    [Fact]
    public async Task A_reported_meeting_can_be_disputed_by_cancelling_with_a_reason()
    {
        var (ali, ayse, matchPath) = await MatchedPairAsync();
        var path = PathOf(await OkAsync(PostAsync(ali, $"{matchPath}/sessions/in-person"), HttpStatusCode.Created));

        await ProblemAssert.IsAsync(await PostAsync(ayse, $"{path}/cancel", new { reason = "yok" }), HttpStatusCode.BadRequest, "validation_failed");
        await ProblemAssert.IsAsync(await PostAsync(ayse, $"{path}/cancel", new { }), HttpStatusCode.BadRequest, "validation_failed");
        var cancelled = await OkAsync(PostAsync(ayse, $"{path}/cancel", new { reason = " Böyle bir buluşma olmadı. " }));

        Assert.Equal("Cancelled", StatusOf(cancelled));
        Assert.Equal("Böyle bir buluşma olmadı.", cancelled.GetProperty("cancelReason").GetString());
        Assert.Equal(ayse.UserId, cancelled.GetProperty("cancelledByUserId").GetGuid());
        await ProblemAssert.IsAsync(await PostAsync(ayse, $"{path}/completion/confirm"), HttpStatusCode.Conflict, "invalid_state");
    }

    [Fact]
    public async Task Proposals_are_validated_and_hidden_from_strangers()
    {
        var (ali, ayse, matchPath) = await MatchedPairAsync();
        var stranger = await TestSession.RegisterVisibleAsync(_client, fixture);
        var path = PathOf(await OkAsync(ProposeAsync(ali, matchPath), HttpStatusCode.Created));

        await ProblemAssert.IsAsync(
            await ProposeAsync(ali, matchPath, DateTimeOffset.UtcNow.AddMinutes(-5)), HttpStatusCode.Conflict, "session_in_past");
        await ProblemAssert.IsAsync(await ProposeAsync(ali, matchPath, duration: 75), HttpStatusCode.BadRequest, "validation_failed");
        await ProblemAssert.IsAsync(
            await ProposeAsync(ali, matchPath, note: new string('a', 501)), HttpStatusCode.BadRequest, "validation_failed");
        await ProblemAssert.IsAsync(await ProposeAsync(stranger, matchPath), HttpStatusCode.NotFound, "not_found");
        await ProblemAssert.IsAsync(await PostAsync(stranger, $"{matchPath}/sessions/in-person"), HttpStatusCode.NotFound, "not_found");
        await ProblemAssert.IsAsync(await stranger.SendAsync(HttpMethod.Get, $"{matchPath}/sessions"), HttpStatusCode.NotFound, "not_found");
        await ProblemAssert.IsAsync(await stranger.SendAsync(HttpMethod.Get, path), HttpStatusCode.NotFound, "not_found");
        await ProblemAssert.IsAsync(await PostAsync(stranger, $"{path}/accept"), HttpStatusCode.NotFound, "not_found");
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/v1/sessions")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync($"/api/v1/users/{ali.UserId}/stats")).StatusCode);

        Assert.Equal("Declined", StatusOf(await OkAsync(PostAsync(ayse, $"{path}/decline"))));
        await ProblemAssert.IsAsync(await PostAsync(ayse, $"{path}/accept"), HttpStatusCode.Conflict, "invalid_state");
    }

    [Fact]
    public async Task Nobody_is_booked_into_two_sessions_at_once_in_any_of_their_matches()
    {
        var (ali, ayse, matchPath) = await MatchedPairAsync();
        var can = await TestSession.RegisterVisibleAsync(_client, fixture);
        var aliCan = await MatchAsync(can, ali);
        var ayseCan = await MatchAsync(can, ayse);
        var start = DateTimeOffset.UtcNow.AddDays(2);

        var first = await OkAsync(ProposeAsync(ali, matchPath, start, 90), HttpStatusCode.Created);
        await ProblemAssert.IsAsync(
            await ProposeAsync(ali, matchPath, start.AddMinutes(60)), HttpStatusCode.Conflict, "session_time_taken");
        await ProblemAssert.IsAsync(
            await ProposeAsync(ali, aliCan, start.AddMinutes(-30), 45), HttpStatusCode.Conflict, "session_time_taken");
        await ProblemAssert.IsAsync(await ProposeAsync(can, aliCan, start), HttpStatusCode.Conflict, "other_user_busy");

        // Ayşe books the time with Can, so Ali's proposal can no longer be accepted.
        var withCan = await OkAsync(ProposeAsync(can, ayseCan, start.AddMinutes(30)), HttpStatusCode.Created);
        await OkAsync(PostAsync(ayse, $"{PathOf(withCan)}/accept"));
        await ProblemAssert.IsAsync(await PostAsync(ayse, $"{PathOf(first)}/accept"), HttpStatusCode.Conflict, "session_time_taken");

        // Right after the end is free, and a cancelled session frees its time.
        await OkAsync(ProposeAsync(ali, aliCan, start.AddMinutes(90)), HttpStatusCode.Created);
        await OkAsync(PostAsync(ayse, $"{PathOf(withCan)}/cancel", new { reason = "Başka bir işim çıktı" }));
        Assert.Equal("Scheduled", StatusOf(await OkAsync(PostAsync(ayse, $"{PathOf(first)}/accept"))));
    }

    [Fact]
    public async Task Sessions_are_listed_per_match_and_across_matches()
    {
        var (ali, ayse, matchPath) = await MatchedPairAsync();
        var later = await OkAsync(ProposeAsync(ali, matchPath, DateTimeOffset.UtcNow.AddDays(5)), HttpStatusCode.Created);
        var sooner = await OkAsync(ProposeAsync(ayse, matchPath, DateTimeOffset.UtcNow.AddDays(1)), HttpStatusCode.Created);
        await OkAsync(PostAsync(ali, $"{PathOf(sooner)}/accept"));

        var perMatch = await ayse.GetJsonAsync($"{matchPath}/sessions");
        var scheduled = await ali.GetJsonAsync("/api/v1/sessions?status=Scheduled");
        var soon = await ali.GetJsonAsync(
            $"/api/v1/sessions?to={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(3).ToString("O"))}");

        Assert.Equal(
            [later.GetProperty("id").GetGuid(), sooner.GetProperty("id").GetGuid()],
            perMatch.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()));
        Assert.Equal(sooner.GetProperty("id").GetGuid(), Assert.Single(scheduled.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.Equal(1, soon.GetProperty("totalCount").GetInt32());
        await ProblemAssert.IsAsync(
            await ali.SendAsync(HttpMethod.Get, "/api/v1/sessions?pageSize=51"), HttpStatusCode.BadRequest, "validation_failed");
    }

    [Fact]
    public async Task Deadlines_decline_ignored_proposals_and_complete_unanswered_marks()
    {
        var (ali, ayse, matchPath) = await MatchedPairAsync();
        var ignored = await OkAsync(ProposeAsync(ali, matchPath), HttpStatusCode.Created);
        var reported = await OkAsync(PostAsync(ali, $"{matchPath}/sessions/in-person"), HttpStatusCode.Created);
        await ExecuteSqlAsync(
            $"UPDATE sessions.swap_sessions SET scheduled_at = now() - interval '1 minute' WHERE id = {ignored.GetProperty("id").GetGuid()}");
        await ExecuteSqlAsync(
            $"UPDATE sessions.swap_sessions SET completion_confirm_deadline = now() - interval '1 minute' WHERE id = {reported.GetProperty("id").GetGuid()}");

        // The outcome shows before the background job has recorded it.
        Assert.Equal("Declined", StatusOf(await ayse.GetJsonAsync(PathOf(ignored))));
        var completed = await ayse.GetJsonAsync(PathOf(reported));
        Assert.Equal("Completed", StatusOf(completed));
        Assert.True(completed.GetProperty("completedAutomatically").GetBoolean());
        await ProblemAssert.IsAsync(await PostAsync(ayse, $"{PathOf(ignored)}/accept"), HttpStatusCode.Conflict, "invalid_state");
        await ProblemAssert.IsAsync(
            await PostAsync(ayse, $"{PathOf(reported)}/cancel", new { reason = "Buluşmadık ki" }), HttpStatusCode.Conflict, "invalid_state");
        Assert.Equal(1, (await ali.GetJsonAsync("/api/v1/sessions?status=Completed")).GetProperty("totalCount").GetInt32());

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            Assert.True(await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ProcessSessionDeadlinesCommand()) >= 2);
        }

        var recorded = await ayse.GetJsonAsync(PathOf(reported));
        Assert.Equal("Completed", StatusOf(recorded));
        Assert.True(recorded.GetProperty("completedAutomatically").GetBoolean());
        Assert.Equal("Declined", StatusOf(await ali.GetJsonAsync(PathOf(ignored))));
    }

    [Fact]
    public async Task A_match_that_is_ending_takes_no_new_sessions_and_ending_it_cancels_the_ones_ahead()
    {
        var (ali, ayse, matchPath) = await MatchedPairAsync();
        var proposed = await OkAsync(ProposeAsync(ali, matchPath), HttpStatusCode.Created);
        var scheduled = await OkAsync(ProposeAsync(ali, matchPath, DateTimeOffset.UtcNow.AddDays(3)), HttpStatusCode.Created);
        await OkAsync(PostAsync(ayse, $"{PathOf(scheduled)}/accept"));
        var reported = await OkAsync(PostAsync(ali, $"{matchPath}/sessions/in-person"), HttpStatusCode.Created);

        await OkAsync(PostAsync(ali, $"{matchPath}/end-request"));
        await ProblemAssert.IsAsync(await ProposeAsync(ali, matchPath), HttpStatusCode.Conflict, "match_end_pending");
        await ProblemAssert.IsAsync(
            await PostAsync(ayse, $"{matchPath}/sessions/in-person"), HttpStatusCode.Conflict, "match_end_pending");

        await OkAsync(PostAsync(ayse, $"{matchPath}/end-request/accept"));

        await ProblemAssert.IsAsync(await ProposeAsync(ali, matchPath), HttpStatusCode.Conflict, "invalid_state");
        var cancelled = await ali.GetJsonAsync(PathOf(scheduled));
        Assert.Equal("Cancelled", StatusOf(cancelled));
        Assert.Equal(JsonValueKind.Null, cancelled.GetProperty("cancelledByUserId").ValueKind);
        Assert.Equal("Cancelled", StatusOf(await ali.GetJsonAsync(PathOf(proposed))));
        // The meeting that already happened can still be confirmed after the match ended.
        Assert.Equal("Completed", StatusOf(await OkAsync(PostAsync(ayse, $"{PathOf(reported)}/completion/confirm"))));
    }

    [Fact]
    public async Task Statistics_tell_completed_matches_from_those_that_ended_without_a_session()
    {
        var (ali, ayse, firstMatch) = await MatchedPairAsync();
        var viewer = await TestSession.RegisterAsync(_client);
        Assert.Equal((0, 0, 0, 0), await StatsAsync(viewer, ali));

        // First match: one completed meeting, then the match ends.
        var reported = await OkAsync(PostAsync(ali, $"{firstMatch}/sessions/in-person"), HttpStatusCode.Created);
        await OkAsync(PostAsync(ayse, $"{PathOf(reported)}/completion/confirm"));
        Assert.Equal((0, 0, 0, 1), await StatsAsync(viewer, ali));
        await EndMatchAsync(ali, ayse, firstMatch);
        Assert.Equal((1, 1, 0, 1), await StatsAsync(viewer, ali));

        // Second match with someone else ends without any session.
        var carol = await TestSession.RegisterVisibleAsync(_client, fixture);
        var guitar = await ali.FindSkillIdAsync("gitar");
        var request = await OkAsync(
            carol.SendAsync(
                HttpMethod.Post,
                "/api/v1/swap-requests",
                new { recipientId = ali.UserId, offeredSkillId = guitar, requestedSkillId = guitar }),
            HttpStatusCode.Created);
        var second = await OkAsync(PostAsync(ali, $"/api/v1/swap-requests/{request.GetProperty("id").GetGuid()}/accept"));
        Assert.Equal((1, 1, 0, 1), await StatsAsync(viewer, ali));
        await EndMatchAsync(carol, ali, $"/api/v1/matches/{second.GetProperty("id").GetGuid()}");

        Assert.Equal((2, 1, 1, 1), await StatsAsync(viewer, ali));
        Assert.Equal((1, 1, 0, 1), await StatsAsync(ali, ayse));
        Assert.Equal((1, 0, 1, 0), await StatsAsync(ali, carol));
        Assert.Equal((0, 0, 0, 0), await StatsAsync(ali, viewer));
        await ProblemAssert.IsAsync(
            await ali.SendAsync(HttpMethod.Get, $"/api/v1/users/{Guid.NewGuid()}/stats"), HttpStatusCode.NotFound, "not_found");
    }
}
