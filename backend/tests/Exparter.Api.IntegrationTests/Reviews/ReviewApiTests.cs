using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Exparter.Api.IntegrationTests.Reviews;

public class ReviewApiTests(ApiFixture fixture) : IClassFixture<ApiFixture>
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

    private static Task<HttpResponseMessage> ReviewAsync(TestSession reviewer, TestSession reviewee, int rating = 8, string? comment = null) =>
        PostAsync(reviewer, "/api/v1/reviews", new { revieweeId = reviewee.UserId, rating, comment });

    /// <summary>Matches the two users. Returns the path of the match.</summary>
    private static async Task<string> MatchAsync(TestSession first, TestSession second)
    {
        var guitar = await first.FindSkillIdAsync("gitar");
        var request = await OkAsync(
            PostAsync(first, "/api/v1/swap-requests", new { recipientId = second.UserId, offeredSkillId = guitar, requestedSkillId = guitar }),
            HttpStatusCode.Created);
        var match = await OkAsync(PostAsync(second, $"/api/v1/swap-requests/{request.GetProperty("id").GetGuid()}/accept"));
        return $"/api/v1/matches/{match.GetProperty("id").GetGuid()}";
    }

    /// <summary>The two users meet in person and both say so.</summary>
    private static async Task<Guid> CompleteSessionAsync(TestSession first, TestSession second, string matchPath)
    {
        var reported = await OkAsync(PostAsync(first, $"{matchPath}/sessions/in-person"), HttpStatusCode.Created);
        var sessionId = reported.GetProperty("id").GetGuid();
        await OkAsync(PostAsync(second, $"/api/v1/sessions/{sessionId}/completion/confirm"));
        return sessionId;
    }

    /// <summary>Two users with a completed session in a match that is still running.</summary>
    private async Task<(TestSession Ali, TestSession Ayse, Guid SessionId)> PairWithCompletedSessionAsync()
    {
        var ali = await TestSession.RegisterVisibleAsync(_client, fixture);
        var ayse = await TestSession.RegisterVisibleAsync(_client, fixture);
        return (ali, ayse, await CompleteSessionAsync(ali, ayse, await MatchAsync(ali, ayse)));
    }

    private static async Task<(bool CanReview, string? Reason)> EligibilityAsync(TestSession user, TestSession other)
    {
        var eligibility = await user.GetJsonAsync($"/api/v1/users/{other.UserId}/review-eligibility");
        return (eligibility.GetProperty("canReview").GetBoolean(), eligibility.GetProperty("reason").GetString());
    }

    private static (int Count, double? Average) RatingOf(JsonElement resource)
    {
        var rating = resource.GetProperty("rating");
        var average = rating.GetProperty("averageRating");
        return (rating.GetProperty("reviewCount").GetInt32(), average.ValueKind == JsonValueKind.Null ? null : average.GetDouble());
    }

    [Fact]
    public async Task A_review_is_written_after_a_completed_session_and_shows_on_the_profile()
    {
        var (ali, ayse, sessionId) = await PairWithCompletedSessionAsync();
        Assert.Equal((true, null), await EligibilityAsync(ali, ayse));

        var review = await OkAsync(ReviewAsync(ali, ayse, 9, "  Çok sabırlı.  "), HttpStatusCode.Created);

        Assert.Equal(9, review.GetProperty("rating").GetInt32());
        Assert.Equal("Çok sabırlı.", review.GetProperty("comment").GetString());
        Assert.Equal(ayse.UserId, review.GetProperty("revieweeId").GetGuid());
        Assert.Equal(sessionId, review.GetProperty("sessionId").GetGuid());
        Assert.Equal(ali.Username, review.GetProperty("reviewer").GetProperty("username").GetString());

        // The other side reviews back; both count at once, everywhere a rating is shown.
        await OkAsync(ReviewAsync(ayse, ali, 6), HttpStatusCode.Created);
        var viewer = await TestSession.RegisterAsync(_client);
        Assert.Equal((1, 9d), RatingOf(await viewer.GetJsonAsync($"/api/v1/users/{ayse.UserId}")));
        Assert.Equal((1, 6d), RatingOf(await ali.GetJsonAsync("/api/v1/me/profile")));
        var found = await viewer.GetJsonAsync($"/api/v1/discovery/users?q={ayse.Username}");
        Assert.Equal((1, 9d), RatingOf(Assert.Single(found.GetProperty("items").EnumerateArray())));

        var received = await viewer.GetJsonAsync($"/api/v1/users/{ayse.UserId}/reviews");
        Assert.Equal(1, received.GetProperty("totalCount").GetInt32());
        Assert.Equal(review.GetProperty("id").GetGuid(), received.GetProperty("items")[0].GetProperty("id").GetGuid());
        var given = await ali.GetJsonAsync("/api/v1/me/reviews/given");
        Assert.Equal(ayse.UserId, Assert.Single(given.GetProperty("items").EnumerateArray()).GetProperty("revieweeId").GetGuid());
    }

    [Fact]
    public async Task A_user_is_reviewed_only_once_and_a_deleted_review_cannot_be_replaced()
    {
        var (ali, ayse, _) = await PairWithCompletedSessionAsync();
        var review = await OkAsync(ReviewAsync(ali, ayse, 3), HttpStatusCode.Created);
        var path = $"/api/v1/reviews/{review.GetProperty("id").GetGuid()}";

        await ProblemAssert.IsAsync(await ReviewAsync(ali, ayse, 10), HttpStatusCode.Conflict, "review_already_exists");
        Assert.Equal((false, "AlreadyReviewed"), await EligibilityAsync(ali, ayse));
        await ProblemAssert.IsAsync(await ayse.SendAsync(HttpMethod.Delete, path), HttpStatusCode.Forbidden, "not_participant");

        Assert.Equal(HttpStatusCode.NoContent, (await ali.SendAsync(HttpMethod.Delete, path)).StatusCode);

        Assert.Equal((0, null), RatingOf(await ali.GetJsonAsync($"/api/v1/users/{ayse.UserId}")));
        Assert.Equal(0, (await ali.GetJsonAsync($"/api/v1/users/{ayse.UserId}/reviews")).GetProperty("totalCount").GetInt32());
        Assert.Equal(0, (await ali.GetJsonAsync("/api/v1/me/reviews/given")).GetProperty("totalCount").GetInt32());
        await ProblemAssert.IsAsync(await ali.SendAsync(HttpMethod.Delete, path), HttpStatusCode.NotFound, "not_found");
        await ProblemAssert.IsAsync(await ReviewAsync(ali, ayse, 10), HttpStatusCode.Conflict, "review_already_exists");
        Assert.Equal((false, "AlreadyReviewed"), await EligibilityAsync(ali, ayse));
    }

    [Fact]
    public async Task A_review_is_refused_without_a_completed_session_and_for_invalid_input()
    {
        var ali = await TestSession.RegisterVisibleAsync(_client, fixture);
        var ayse = await TestSession.RegisterVisibleAsync(_client, fixture);
        var matchPath = await MatchAsync(ali, ayse);

        // A match alone is not enough, and neither is a meeting the other side has not confirmed.
        await ProblemAssert.IsAsync(await ReviewAsync(ali, ayse), HttpStatusCode.Conflict, "session_not_completed");
        await OkAsync(PostAsync(ali, $"{matchPath}/sessions/in-person"), HttpStatusCode.Created);
        await ProblemAssert.IsAsync(await ReviewAsync(ayse, ali), HttpStatusCode.Conflict, "session_not_completed");
        Assert.Equal((false, "NoCompletedSession"), await EligibilityAsync(ali, ayse));
        Assert.Equal((false, "Self"), await EligibilityAsync(ali, ali));

        await ProblemAssert.IsAsync(await ReviewAsync(ali, ayse, 0), HttpStatusCode.BadRequest, "validation_failed");
        await ProblemAssert.IsAsync(await ReviewAsync(ali, ayse, 11), HttpStatusCode.BadRequest, "validation_failed");
        await ProblemAssert.IsAsync(
            await ReviewAsync(ali, ayse, 5, new string('a', 501)), HttpStatusCode.BadRequest, "validation_failed");
        await ProblemAssert.IsAsync(await ReviewAsync(ali, ali), HttpStatusCode.Conflict, "review_of_self");
        await ProblemAssert.IsAsync(
            await PostAsync(ali, "/api/v1/reviews", new { revieweeId = Guid.NewGuid(), rating = 5 }), HttpStatusCode.NotFound, "not_found");

        var newcomer = await TestSession.RegisterAsync(_client);
        await ProblemAssert.IsAsync(await ReviewAsync(newcomer, ayse), HttpStatusCode.Forbidden, "interaction_requirements_not_met");

        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync($"/api/v1/users/{ali.UserId}/reviews")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PostAsJsonAsync("/api/v1/reviews", new { })).StatusCode);
        await ProblemAssert.IsAsync(
            await ali.SendAsync(HttpMethod.Get, $"/api/v1/users/{Guid.NewGuid()}/reviews"), HttpStatusCode.NotFound, "not_found");
        await ProblemAssert.IsAsync(
            await ali.SendAsync(HttpMethod.Get, $"/api/v1/users/{Guid.NewGuid()}/review-eligibility"), HttpStatusCode.NotFound, "not_found");
        await ProblemAssert.IsAsync(
            await ali.SendAsync(HttpMethod.Get, $"/api/v1/users/{ayse.UserId}/reviews?pageSize=51"), HttpStatusCode.BadRequest, "validation_failed");
    }

    [Fact]
    public async Task Discovery_ranks_equally_compatible_users_by_their_average_rating()
    {
        // A city nobody else in the tests lives in, so that only these users are listed.
        const string city = "77";
        var viewer = await TestSession.RegisterVisibleAsync(_client, fixture);
        var (reviewerOne, wellRated, _) = await PairWithCompletedSessionAsync();
        var (reviewerTwo, poorlyRated, _) = await PairWithCompletedSessionAsync();
        var unrated = await TestSession.RegisterVisibleAsync(_client, fixture);
        await OkAsync(ReviewAsync(reviewerTwo, poorlyRated, 4), HttpStatusCode.Created);
        await OkAsync(ReviewAsync(reviewerOne, wellRated, 9), HttpStatusCode.Created);

        // Moved in the reverse of the expected order: recency alone would list them backwards.
        foreach (var user in new[] { wellRated, poorlyRated, unrated })
        {
            await OkAsync(user.SendAsync(
                HttpMethod.Put,
                "/api/v1/me/profile",
                new { displayName = "Test Kullanıcı", city, acceptsOnline = true, acceptsInPerson = true }));
        }

        var listed = await viewer.GetJsonAsync($"/api/v1/discovery/users?city={city}");

        Assert.Equal(
            [wellRated.UserId, poorlyRated.UserId, unrated.UserId],
            listed.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("user").GetProperty("userId").GetGuid()));
    }
}
