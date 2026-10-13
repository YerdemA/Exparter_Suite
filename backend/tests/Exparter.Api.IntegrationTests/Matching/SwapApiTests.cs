using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Exparter.Application.Matching;
using Exparter.Infrastructure.Persistence;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Exparter.Api.IntegrationTests.Matching;

public class SwapApiTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private readonly HttpClient _client = fixture.CreateClient();

    /// <summary>Two users who can interact: the first offers the guitar, the second Spanish.</summary>
    private async Task<(TestSession Ali, TestSession Ayse, Guid Guitar, Guid Spanish)> PairAsync()
    {
        var ali = await TestSession.RegisterVisibleAsync(_client, fixture);
        var ayse = await TestSession.RegisterVisibleAsync(_client, fixture);
        await ayse.AddSkillAsync("ispanyolca", "Offered", "Expert");
        return (ali, ayse, await ali.FindSkillIdAsync("gitar"), await ali.FindSkillIdAsync("ispanyolca"));
    }

    private static Task<HttpResponseMessage> SendAsync(TestSession from, TestSession to, Guid offered, Guid requested, string? message = null) =>
        from.SendAsync(
            HttpMethod.Post,
            "/api/v1/swap-requests",
            new { recipientId = to.UserId, offeredSkillId = offered, requestedSkillId = requested, message });

    private static async Task<JsonElement> CreatedAsync(Task<HttpResponseMessage> pending)
    {
        var response = await pending;
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<JsonElement> OkAsync(Task<HttpResponseMessage> pending)
    {
        var response = await pending;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static Guid IdOf(JsonElement element) => element.GetProperty("id").GetGuid();

    /// <summary>Moves a stored deadline into the past, standing in for days going by.</summary>
    private async Task ExecuteSqlAsync(FormattableString sql)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlInterpolatedAsync(sql);
    }

    private async Task<int> ProcessDeadlinesAsync()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ProcessMatchingDeadlinesCommand());
    }

    [Fact]
    public async Task A_request_is_sent_listed_for_both_users_and_accepted_into_a_match()
    {
        var (ali, ayse, guitar, spanish) = await PairAsync();

        var request = await CreatedAsync(SendAsync(ali, ayse, guitar, spanish, "  Merhaba!  "));

        Assert.Equal("Pending", request.GetProperty("status").GetString());
        Assert.Equal("Merhaba!", request.GetProperty("message").GetString());
        Assert.Equal(ali.Username, request.GetProperty("requester").GetProperty("username").GetString());
        Assert.Equal("Test Kullanıcı", request.GetProperty("recipient").GetProperty("displayName").GetString());
        Assert.Equal("Gitar", request.GetProperty("offeredSkill").GetProperty("name").GetString());
        Assert.Equal("İspanyolca", request.GetProperty("requestedSkill").GetProperty("name").GetString());
        Assert.Equal(
            request.GetProperty("createdAt").GetDateTimeOffset().AddDays(3),
            request.GetProperty("expiresAt").GetDateTimeOffset());

        var incoming = await ayse.GetJsonAsync("/api/v1/swap-requests?direction=Incoming&status=Pending");
        var outgoing = await ali.GetJsonAsync("/api/v1/swap-requests?direction=Outgoing");
        Assert.Equal(IdOf(request), IdOf(Assert.Single(incoming.GetProperty("items").EnumerateArray())));
        Assert.Equal(1, outgoing.GetProperty("totalCount").GetInt32());
        Assert.Equal(0, (await ali.GetJsonAsync("/api/v1/swap-requests?direction=Incoming")).GetProperty("totalCount").GetInt32());
        Assert.Equal(IdOf(request), IdOf(await ayse.GetJsonAsync($"/api/v1/swap-requests/{IdOf(request)}")));

        var match = await OkAsync(ayse.SendAsync(HttpMethod.Post, $"/api/v1/swap-requests/{IdOf(request)}/accept"));

        Assert.Equal("Active", match.GetProperty("status").GetString());
        Assert.Equal(IdOf(request), match.GetProperty("swapRequestId").GetGuid());
        Assert.Equal(ali.UserId, match.GetProperty("userA").GetProperty("userId").GetGuid());
        Assert.Equal(ayse.UserId, match.GetProperty("userB").GetProperty("userId").GetGuid());
        Assert.Equal("Gitar", match.GetProperty("skillTaughtByA").GetProperty("name").GetString());
        Assert.Equal("İspanyolca", match.GetProperty("skillTaughtByB").GetProperty("name").GetString());
        Assert.NotEqual(JsonValueKind.Null, match.GetProperty("conversationId").ValueKind);

        var matches = await ali.GetJsonAsync("/api/v1/matches?status=Active");
        Assert.Equal(IdOf(match), IdOf(Assert.Single(matches.GetProperty("items").EnumerateArray())));
        Assert.Equal(IdOf(match), IdOf(await ayse.GetJsonAsync($"/api/v1/matches/{IdOf(match)}")));
        Assert.Equal(
            "Accepted",
            (await ali.GetJsonAsync($"/api/v1/swap-requests/{IdOf(request)}")).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Sending_needs_the_interaction_requirements_but_browsing_requests_does_not()
    {
        var (_, ayse, guitar, spanish) = await PairAsync();
        var newcomer = await TestSession.RegisterAsync(_client);

        await ProblemAssert.IsAsync(
            await SendAsync(newcomer, ayse, guitar, spanish), HttpStatusCode.Forbidden, "interaction_requirements_not_met");
        Assert.Equal(0, (await newcomer.GetJsonAsync("/api/v1/swap-requests?direction=Outgoing")).GetProperty("totalCount").GetInt32());
        Assert.Equal(0, (await newcomer.GetJsonAsync("/api/v1/matches")).GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Send_rejects_bad_targets_and_skills()
    {
        var (ali, ayse, guitar, spanish) = await PairAsync();

        await ProblemAssert.IsAsync(await SendAsync(ali, ali, guitar, guitar), HttpStatusCode.Conflict, "swap_request_to_self");
        await ProblemAssert.IsAsync(await SendAsync(ali, ayse, spanish, spanish), HttpStatusCode.Conflict, "skill_not_offered");
        await ProblemAssert.IsAsync(
            await SendAsync(ali, ayse, guitar, await ali.FindSkillIdAsync("yoga")), HttpStatusCode.Conflict, "skill_not_offered");
        await ProblemAssert.IsAsync(
            await ali.SendAsync(
                HttpMethod.Post,
                "/api/v1/swap-requests",
                new { recipientId = Guid.NewGuid(), offeredSkillId = guitar, requestedSkillId = spanish }),
            HttpStatusCode.NotFound,
            "not_found");
        await ProblemAssert.IsAsync(
            await SendAsync(ali, ayse, guitar, spanish, new string('a', 301)), HttpStatusCode.BadRequest, "validation_failed");
        await ProblemAssert.IsAsync(
            await ali.SendAsync(HttpMethod.Get, "/api/v1/swap-requests"), HttpStatusCode.BadRequest, "validation_failed");
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/v1/swap-requests?direction=Incoming")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/v1/matches")).StatusCode);
    }

    [Fact]
    public async Task A_pair_has_one_pending_request_and_then_one_unfinished_match()
    {
        var (ali, ayse, guitar, spanish) = await PairAsync();
        var request = await CreatedAsync(SendAsync(ali, ayse, guitar, spanish));

        await ProblemAssert.IsAsync(await SendAsync(ali, ayse, guitar, spanish), HttpStatusCode.Conflict, "swap_request_already_pending");
        await ProblemAssert.IsAsync(await SendAsync(ayse, ali, spanish, guitar), HttpStatusCode.Conflict, "swap_request_already_pending");

        await OkAsync(ayse.SendAsync(HttpMethod.Post, $"/api/v1/swap-requests/{IdOf(request)}/accept"));

        await ProblemAssert.IsAsync(await SendAsync(ayse, ali, spanish, guitar), HttpStatusCode.Conflict, "match_already_exists");
    }

    [Fact]
    public async Task Only_the_right_user_can_answer_or_withdraw_and_strangers_see_nothing()
    {
        var (ali, ayse, guitar, spanish) = await PairAsync();
        var stranger = await TestSession.RegisterVisibleAsync(_client, fixture);
        var path = $"/api/v1/swap-requests/{IdOf(await CreatedAsync(SendAsync(ali, ayse, guitar, spanish)))}";

        await ProblemAssert.IsAsync(await ali.SendAsync(HttpMethod.Post, $"{path}/accept"), HttpStatusCode.Forbidden, "not_participant");
        await ProblemAssert.IsAsync(await ali.SendAsync(HttpMethod.Post, $"{path}/decline"), HttpStatusCode.Forbidden, "not_participant");
        await ProblemAssert.IsAsync(await ayse.SendAsync(HttpMethod.Post, $"{path}/cancel"), HttpStatusCode.Forbidden, "not_participant");
        await ProblemAssert.IsAsync(await stranger.SendAsync(HttpMethod.Get, path), HttpStatusCode.NotFound, "not_found");
        await ProblemAssert.IsAsync(await stranger.SendAsync(HttpMethod.Post, $"{path}/accept"), HttpStatusCode.NotFound, "not_found");

        var cancelled = await OkAsync(ali.SendAsync(HttpMethod.Post, $"{path}/cancel"));
        Assert.Equal("Cancelled", cancelled.GetProperty("status").GetString());
        await ProblemAssert.IsAsync(await ayse.SendAsync(HttpMethod.Post, $"{path}/accept"), HttpStatusCode.Conflict, "invalid_state");

        // Withdrawing is not being refused: the requester may ask again right away.
        await CreatedAsync(SendAsync(ali, ayse, guitar, spanish));
    }

    [Fact]
    public async Task A_declined_requester_has_to_wait_before_asking_the_same_user_again()
    {
        var (ali, ayse, guitar, spanish) = await PairAsync();
        var request = await CreatedAsync(SendAsync(ali, ayse, guitar, spanish));

        var declined = await OkAsync(ayse.SendAsync(HttpMethod.Post, $"/api/v1/swap-requests/{IdOf(request)}/decline"));
        Assert.Equal("Declined", declined.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, declined.GetProperty("respondedAt").ValueKind);

        await ProblemAssert.IsAsync(await SendAsync(ali, ayse, guitar, spanish), HttpStatusCode.Conflict, "swap_request_cooldown");

        await ExecuteSqlAsync(
            $"UPDATE matching.swap_requests SET responded_at = responded_at - interval '7 days' WHERE id = {IdOf(request)}");
        await CreatedAsync(SendAsync(ali, ayse, guitar, spanish));
    }

    [Fact]
    public async Task An_unanswered_request_expires_and_frees_the_pair()
    {
        var (ali, ayse, guitar, spanish) = await PairAsync();
        var id = IdOf(await CreatedAsync(SendAsync(ali, ayse, guitar, spanish)));
        await ExecuteSqlAsync($"UPDATE matching.swap_requests SET expires_at = now() - interval '1 minute' WHERE id = {id}");

        // Expired for every reader even before the background job has recorded it.
        Assert.Equal("Expired", (await ali.GetJsonAsync($"/api/v1/swap-requests/{id}")).GetProperty("status").GetString());
        Assert.Equal(
            0,
            (await ayse.GetJsonAsync("/api/v1/swap-requests?direction=Incoming&status=Pending")).GetProperty("totalCount").GetInt32());
        await ProblemAssert.IsAsync(
            await ayse.SendAsync(HttpMethod.Post, $"/api/v1/swap-requests/{id}/accept"), HttpStatusCode.Conflict, "invalid_state");

        // The other user can ask while the expiry is still unrecorded.
        await CreatedAsync(SendAsync(ayse, ali, spanish, guitar));
        Assert.Equal(
            1,
            (await ayse.GetJsonAsync("/api/v1/swap-requests?direction=Incoming&status=Expired")).GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task The_background_job_records_passed_deadlines()
    {
        var (ali, ayse, guitar, spanish) = await PairAsync();
        var carol = await TestSession.RegisterVisibleAsync(_client, fixture);
        var requestId = IdOf(await CreatedAsync(SendAsync(ali, carol, guitar, guitar)));
        var match = await OkAsync(ayse.SendAsync(
            HttpMethod.Post,
            $"/api/v1/swap-requests/{IdOf(await CreatedAsync(SendAsync(ali, ayse, guitar, spanish)))}/accept"));
        await OkAsync(ali.SendAsync(HttpMethod.Post, $"/api/v1/matches/{IdOf(match)}/end-request"));

        await ExecuteSqlAsync($"UPDATE matching.swap_requests SET expires_at = now() - interval '1 minute' WHERE id = {requestId}");
        await ExecuteSqlAsync(
            $"UPDATE matching.matches SET end_request_expires_at = now() - interval '1 minute' WHERE id = {IdOf(match)}");
        Assert.True(await ProcessDeadlinesAsync() >= 2);

        var ended = await ayse.GetJsonAsync($"/api/v1/matches/{IdOf(match)}");
        Assert.Equal("Ended", ended.GetProperty("status").GetString());
        Assert.Equal("EndRequestExpired", ended.GetProperty("endReason").GetString());
        Assert.Equal(ali.UserId, ended.GetProperty("endRequestedByUserId").GetGuid());
        var expired = await carol.GetJsonAsync($"/api/v1/swap-requests/{requestId}");
        Assert.Equal("Expired", expired.GetProperty("status").GetString());
        Assert.Equal(expired.GetProperty("expiresAt").GetDateTimeOffset(), expired.GetProperty("respondedAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task A_match_ends_by_mutual_consent_and_the_pair_can_match_again()
    {
        var (ali, ayse, guitar, spanish) = await PairAsync();
        var stranger = await TestSession.RegisterVisibleAsync(_client, fixture);
        var request = await CreatedAsync(SendAsync(ali, ayse, guitar, spanish));
        var path = $"/api/v1/matches/{IdOf(await OkAsync(ayse.SendAsync(HttpMethod.Post, $"/api/v1/swap-requests/{IdOf(request)}/accept")))}";

        await ProblemAssert.IsAsync(await stranger.SendAsync(HttpMethod.Get, path), HttpStatusCode.NotFound, "not_found");
        await ProblemAssert.IsAsync(
            await ayse.SendAsync(HttpMethod.Post, $"{path}/end-request/accept"), HttpStatusCode.Conflict, "invalid_state");

        var waiting = await OkAsync(ali.SendAsync(HttpMethod.Post, $"{path}/end-request"));
        Assert.Equal("EndRequested", waiting.GetProperty("status").GetString());
        Assert.Equal(ali.UserId, waiting.GetProperty("endRequestedByUserId").GetGuid());
        Assert.Equal(
            waiting.GetProperty("endRequestedAt").GetDateTimeOffset().AddDays(7),
            waiting.GetProperty("endRequestExpiresAt").GetDateTimeOffset());
        await ProblemAssert.IsAsync(
            await ali.SendAsync(HttpMethod.Post, $"{path}/end-request/accept"), HttpStatusCode.Forbidden, "not_participant");
        await ProblemAssert.IsAsync(
            await ayse.SendAsync(HttpMethod.Delete, $"{path}/end-request"), HttpStatusCode.Forbidden, "not_participant");

        var rejected = await OkAsync(ayse.SendAsync(HttpMethod.Post, $"{path}/end-request/reject"));
        Assert.Equal("Active", rejected.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, rejected.GetProperty("endRequestedByUserId").ValueKind);

        await OkAsync(ayse.SendAsync(HttpMethod.Post, $"{path}/end-request"));
        var withdrawn = await OkAsync(ayse.SendAsync(HttpMethod.Delete, $"{path}/end-request"));
        Assert.Equal("Active", withdrawn.GetProperty("status").GetString());

        await OkAsync(ali.SendAsync(HttpMethod.Post, $"{path}/end-request"));
        var ended = await OkAsync(ayse.SendAsync(HttpMethod.Post, $"{path}/end-request/accept"));
        Assert.Equal("Ended", ended.GetProperty("status").GetString());
        Assert.Equal("MutualConsent", ended.GetProperty("endReason").GetString());
        Assert.NotEqual(JsonValueKind.Null, ended.GetProperty("endedAt").ValueKind);
        await ProblemAssert.IsAsync(await ali.SendAsync(HttpMethod.Post, $"{path}/end-request"), HttpStatusCode.Conflict, "invalid_state");

        await CreatedAsync(SendAsync(ayse, ali, spanish, guitar));
        Assert.Equal(1, (await ali.GetJsonAsync("/api/v1/matches?status=Ended")).GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task An_offered_skill_cannot_be_removed_while_a_request_or_match_relies_on_it()
    {
        var (ali, ayse, guitar, spanish) = await PairAsync();
        var skills = await ali.GetJsonAsync("/api/v1/me/skills?type=Offered");
        var removeGuitar = $"/api/v1/me/skills/{IdOf(skills.GetProperty("items")[0])}";
        var request = await CreatedAsync(SendAsync(ali, ayse, guitar, spanish));

        await ProblemAssert.IsAsync(await ali.SendAsync(HttpMethod.Delete, removeGuitar), HttpStatusCode.Conflict, "user_skill_in_use");

        var path = $"/api/v1/matches/{IdOf(await OkAsync(ayse.SendAsync(HttpMethod.Post, $"/api/v1/swap-requests/{IdOf(request)}/accept")))}";
        await ProblemAssert.IsAsync(await ali.SendAsync(HttpMethod.Delete, removeGuitar), HttpStatusCode.Conflict, "user_skill_in_use");

        await OkAsync(ali.SendAsync(HttpMethod.Post, $"{path}/end-request"));
        await OkAsync(ayse.SendAsync(HttpMethod.Post, $"{path}/end-request/accept"));
        Assert.Equal(HttpStatusCode.NoContent, (await ali.SendAsync(HttpMethod.Delete, removeGuitar)).StatusCode);
    }

    [Fact]
    public async Task The_database_itself_refuses_a_second_pending_request_for_a_pair()
    {
        var (ali, ayse, guitar, spanish) = await PairAsync();
        var id = IdOf(await CreatedAsync(SendAsync(ali, ayse, guitar, spanish)));

        // What two requests racing past the handler's own check would run into.
        var exception = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => ExecuteSqlAsync(
            $"""
             INSERT INTO matching.swap_requests (id, requester_id, recipient_id, offered_skill_id, requested_skill_id, status, created_at, expires_at)
             SELECT gen_random_uuid(), recipient_id, requester_id, requested_skill_id, offered_skill_id, 'Pending', created_at, expires_at
             FROM matching.swap_requests WHERE id = {id}
             """));

        Assert.Equal("ix_swap_requests_pending_pair", exception.ConstraintName);
    }
}
