using System.Net;
using System.Text.Json;

namespace Exparter.Api.IntegrationTests.Matching;

/// <summary>
/// The database is shared with every other test, so each test here puts its users in a
/// city of its own and filters by it (or searches for a name only it uses).
/// </summary>
public class DiscoveryApiTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private readonly HttpClient _client = fixture.CreateClient();

    private async Task<TestSession> RegisterAsync(
        string city,
        string displayName = "Test Kullanıcı",
        bool verified = true,
        bool acceptsOnline = false)
    {
        var session = await TestSession.RegisterAsync(_client);
        if (verified)
        {
            await session.VerifyEmailAsync(fixture);
        }

        var response = await session.SendAsync(
            HttpMethod.Put,
            "/api/v1/me/profile",
            new { displayName, city, acceptsOnline, acceptsInPerson = true });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return session;
    }

    private static List<JsonElement> Items(JsonElement page) => page.GetProperty("items").EnumerateArray().ToList();

    private static Guid IdOf(JsonElement item) => item.GetProperty("user").GetProperty("userId").GetGuid();

    private static string CompatibilityOf(List<JsonElement> items, TestSession user) =>
        items.Single(item => IdOf(item) == user.UserId).GetProperty("compatibility").GetString()!;

    [Fact]
    public async Task Suggestions_rank_mutual_then_one_way_and_ignore_skills_below_the_wanted_level()
    {
        const string city = "71";
        var viewer = await RegisterAsync(city);
        await viewer.AddSkillAsync("gitar", "Offered", "Advanced");
        await viewer.AddSkillAsync("ispanyolca", "Wanted", "Intermediate");

        var unrelated = await RegisterAsync(city);
        await unrelated.AddSkillAsync("yoga", "Offered", "Expert");

        var belowLevel = await RegisterAsync(city);
        await belowLevel.AddSkillAsync("ispanyolca", "Offered", "Beginner");

        var oneWay = await RegisterAsync(city);
        await oneWay.AddSkillAsync("ispanyolca", "Offered", "Intermediate");

        var mutual = await RegisterAsync(city);
        await mutual.AddSkillAsync("ispanyolca", "Offered", "Expert");
        await mutual.AddSkillAsync("gitar", "Wanted", "Advanced");

        // Wants more than the viewer can teach, so only one direction fits.
        var wantsTooMuch = await RegisterAsync(city);
        await wantsTooMuch.AddSkillAsync("ispanyolca", "Offered", "Advanced");
        await wantsTooMuch.AddSkillAsync("gitar", "Wanted", "Expert");

        var items = Items(await viewer.GetJsonAsync($"/api/v1/discovery/users?city={city}"));

        Assert.Equal(5, items.Count);
        Assert.Equal(mutual.UserId, IdOf(items[0]));
        Assert.Equal(
            new[] { oneWay.UserId, wantsTooMuch.UserId }.Order(),
            new[] { IdOf(items[1]), IdOf(items[2]) }.Order());
        Assert.Equal("Mutual", CompatibilityOf(items, mutual));
        Assert.Equal("OneWay", CompatibilityOf(items, oneWay));
        Assert.Equal("OneWay", CompatibilityOf(items, wantsTooMuch));
        Assert.Equal("None", CompatibilityOf(items, belowLevel));
        Assert.Equal("None", CompatibilityOf(items, unrelated));

        var first = items[0];
        Assert.Equal(mutual.Username, first.GetProperty("user").GetProperty("username").GetString());
        Assert.Equal(city, first.GetProperty("city").GetString());
        Assert.Equal("İspanyolca", Assert.Single(first.GetProperty("offeredSkills").EnumerateArray()).GetProperty("skill").GetProperty("name").GetString());
        Assert.Equal("Advanced", Assert.Single(first.GetProperty("wantedSkills").EnumerateArray()).GetProperty("level").GetString());
        Assert.Equal(0, first.GetProperty("rating").GetProperty("reviewCount").GetInt32());
    }

    [Fact]
    public async Task Suggestions_leave_out_the_viewer_and_users_who_cannot_interact_yet()
    {
        const string city = "72";
        var viewer = await RegisterAsync(city);
        await viewer.AddSkillAsync("gitar", "Offered", "Advanced");

        var ready = await RegisterAsync(city);
        await ready.AddSkillAsync("yoga", "Offered", "Advanced");

        var unverified = await RegisterAsync(city, verified: false);
        await unverified.AddSkillAsync("yoga", "Offered", "Advanced");

        var nothingToOffer = await RegisterAsync(city);
        await nothingToOffer.AddSkillAsync("yoga", "Wanted", "Beginner");

        var items = Items(await viewer.GetJsonAsync($"/api/v1/discovery/users?city={city}"));

        Assert.Equal(ready.UserId, IdOf(Assert.Single(items)));
    }

    [Fact]
    public async Task A_user_who_cannot_interact_yet_still_gets_suggestions()
    {
        const string city = "73";
        var ready = await RegisterAsync(city);
        await ready.AddSkillAsync("yoga", "Offered", "Advanced");
        var newcomer = await TestSession.RegisterAsync(_client);

        var items = Items(await newcomer.GetJsonAsync($"/api/v1/discovery/users?city={city}"));

        Assert.Equal(ready.UserId, IdOf(Assert.Single(items)));
        Assert.Equal("None", CompatibilityOf(items, ready));
    }

    [Fact]
    public async Task Search_finds_users_by_username_or_name_even_if_they_cannot_interact_yet()
    {
        var viewer = await TestSession.RegisterAsync(_client);
        var marker = Guid.NewGuid().ToString("N")[..8];
        var named = await RegisterAsync("74", displayName: $"Işık Çağrı {marker}", verified: false);
        var blank = await TestSession.RegisterAsync(_client);

        var byName = Items(await viewer.GetJsonAsync($"/api/v1/discovery/users?q={Uri.EscapeDataString($"ISIK cagri {marker}")}"));
        var byUsername = Items(await viewer.GetJsonAsync($"/api/v1/discovery/users?q={blank.Username.ToUpperInvariant()}"));

        Assert.Equal(named.UserId, IdOf(Assert.Single(byName)));
        var found = Assert.Single(byUsername);
        Assert.Equal(blank.UserId, IdOf(found));
        Assert.Equal(JsonValueKind.Null, found.GetProperty("user").GetProperty("displayName").ValueKind);
    }

    [Fact]
    public async Task Search_treats_wildcard_characters_as_plain_text()
    {
        var viewer = await TestSession.RegisterAsync(_client);

        var items = Items(await viewer.GetJsonAsync("/api/v1/discovery/users?q=%25_%25"));

        Assert.Empty(items);
    }

    [Fact]
    public async Task Filters_narrow_suggestions_by_offered_skill_category_and_mode()
    {
        const string city = "75";
        var viewer = await TestSession.RegisterAsync(_client);
        var guitarist = await RegisterAsync(city, acceptsOnline: true);
        await guitarist.AddSkillAsync("gitar", "Offered", "Advanced");
        var yogi = await RegisterAsync(city);
        await yogi.AddSkillAsync("yoga", "Offered", "Advanced");
        await yogi.AddSkillAsync("gitar", "Wanted", "Beginner");

        var guitarId = await viewer.FindSkillIdAsync("gitar");
        var skills = await viewer.GetJsonAsync("/api/v1/skills?q=gitar");
        var categoryId = skills.GetProperty("items")[0].GetProperty("categoryId").GetGuid();

        var bySkill = Items(await viewer.GetJsonAsync($"/api/v1/discovery/users?city={city}&skillId={guitarId}"));
        var byCategory = Items(await viewer.GetJsonAsync($"/api/v1/discovery/users?city={city}&categoryId={categoryId}"));
        var online = Items(await viewer.GetJsonAsync($"/api/v1/discovery/users?city={city}&mode=Online"));
        var inPerson = Items(await viewer.GetJsonAsync($"/api/v1/discovery/users?city={city}&mode=InPerson"));

        Assert.Equal(guitarist.UserId, IdOf(Assert.Single(bySkill)));
        Assert.Equal(guitarist.UserId, IdOf(Assert.Single(byCategory)));
        Assert.Equal(guitarist.UserId, IdOf(Assert.Single(online)));
        Assert.Equal(2, inPerson.Count);
    }

    [Fact]
    public async Task Pages_follow_each_other_through_the_cursor()
    {
        const string city = "76";
        var viewer = await TestSession.RegisterAsync(_client);
        var expected = new List<Guid>();
        for (var index = 0; index < 3; index++)
        {
            var user = await RegisterAsync(city);
            await user.AddSkillAsync("yoga", "Offered", "Advanced");
            expected.Add(user.UserId);
        }

        var first = await viewer.GetJsonAsync($"/api/v1/discovery/users?city={city}&limit=2");
        var cursor = first.GetProperty("nextCursor").GetString();
        var second = await viewer.GetJsonAsync($"/api/v1/discovery/users?city={city}&limit=2&cursor={cursor}");

        Assert.Equal(2, Items(first).Count);
        Assert.Equal(JsonValueKind.Null, second.GetProperty("nextCursor").ValueKind);
        Assert.Equal(expected.Order(), Items(first).Concat(Items(second)).Select(IdOf).Order());
    }

    [Fact]
    public async Task Discovery_requires_a_signed_in_user_and_valid_parameters()
    {
        var viewer = await TestSession.RegisterAsync(_client);

        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/v1/discovery/users")).StatusCode);
        await ProblemAssert.IsAsync(
            await viewer.SendAsync(HttpMethod.Get, "/api/v1/discovery/users?city=99"), HttpStatusCode.BadRequest, "validation_failed");
        await ProblemAssert.IsAsync(
            await viewer.SendAsync(HttpMethod.Get, "/api/v1/discovery/users?limit=51"), HttpStatusCode.BadRequest, "validation_failed");
        await ProblemAssert.IsAsync(
            await viewer.SendAsync(HttpMethod.Get, "/api/v1/discovery/users?cursor=abc"), HttpStatusCode.BadRequest, "validation_failed");
    }
}
