using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Exparter.Api.IntegrationTests.Profiles;

[Collection(ApiCollection.Name)]
public class ProfileApiTests(ApiFixture fixture)
{
    private readonly HttpClient _client = fixture.CreateClient();

    private static object ValidProfile => new
    {
        displayName = "Ayşe Yılmaz",
        bio = "Gitar çalıyorum.",
        city = "34",
        acceptsOnline = true,
        acceptsInPerson = true,
    };

    [Fact]
    public async Task A_new_account_has_a_blank_incomplete_profile()
    {
        var session = await TestSession.RegisterAsync(_client);

        var profile = await session.GetJsonAsync("/api/v1/me/profile");

        Assert.Equal(session.UserId, profile.GetProperty("userId").GetGuid());
        Assert.Equal(JsonValueKind.Null, profile.GetProperty("displayName").ValueKind);
        Assert.Equal(JsonValueKind.Null, profile.GetProperty("avatarUrl").ValueKind);
        Assert.False(profile.GetProperty("isComplete").GetBoolean());
        Assert.Equal(0, profile.GetProperty("rating").GetProperty("reviewCount").GetInt32());
    }

    [Fact]
    public async Task Updating_the_profile_completes_it()
    {
        var session = await TestSession.RegisterAsync(_client);

        var response = await session.SendAsync(HttpMethod.Put, "/api/v1/me/profile", ValidProfile);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await session.GetJsonAsync("/api/v1/me/profile");
        Assert.Equal("Ayşe Yılmaz", profile.GetProperty("displayName").GetString());
        Assert.Equal("Gitar çalıyorum.", profile.GetProperty("bio").GetString());
        Assert.Equal("34", profile.GetProperty("city").GetString());
        Assert.True(profile.GetProperty("acceptsInPerson").GetBoolean());
        Assert.True(profile.GetProperty("isComplete").GetBoolean());
    }

    [Fact]
    public async Task Updating_the_profile_reports_validation_errors_per_field()
    {
        var session = await TestSession.RegisterAsync(_client);

        var response = await session.SendAsync(
            HttpMethod.Put,
            "/api/v1/me/profile",
            new { displayName = "A", city = "99", acceptsOnline = false, acceptsInPerson = false });

        var problem = await ProblemAssert.IsAsync(response, HttpStatusCode.BadRequest, "validation_failed");
        Assert.Equal(
            ["acceptsOnline", "city", "displayName"],
            problem.GetProperty("errors").EnumerateObject().Select(property => property.Name).Order());
    }

    [Fact]
    public async Task Profile_endpoints_require_authentication()
    {
        await ProblemAssert.IsAsync(await _client.GetAsync("/api/v1/me/profile"), HttpStatusCode.Unauthorized, "unauthorized");
        await ProblemAssert.IsAsync(
            await _client.PutAsJsonAsync("/api/v1/me/profile", ValidProfile),
            HttpStatusCode.Unauthorized,
            "unauthorized");
        await ProblemAssert.IsAsync(
            await _client.GetAsync($"/api/v1/users/{Guid.NewGuid()}"),
            HttpStatusCode.Unauthorized,
            "unauthorized");
        await ProblemAssert.IsAsync(await _client.DeleteAsync("/api/v1/me/avatar"), HttpStatusCode.Unauthorized, "unauthorized");
    }

    [Fact]
    public async Task Public_profile_appears_only_once_every_interaction_requirement_is_met()
    {
        var viewer = await TestSession.RegisterAsync(_client);
        var subject = await TestSession.RegisterAsync(_client);
        var path = $"/api/v1/users/{subject.UserId}";

        await ProblemAssert.IsAsync(await viewer.SendAsync(HttpMethod.Get, path), HttpStatusCode.NotFound, "not_found");

        await subject.SendAsync(HttpMethod.Put, "/api/v1/me/profile", ValidProfile);
        await ProblemAssert.IsAsync(await viewer.SendAsync(HttpMethod.Get, path), HttpStatusCode.NotFound, "not_found");

        await subject.VerifyEmailAsync(fixture);
        await ProblemAssert.IsAsync(await viewer.SendAsync(HttpMethod.Get, path), HttpStatusCode.NotFound, "not_found");

        await subject.AddSkillAsync("gitar", "Offered", "Advanced");
        var profile = await viewer.GetJsonAsync(path);

        Assert.Equal(subject.Username, profile.GetProperty("username").GetString());
        Assert.Equal("Ayşe Yılmaz", profile.GetProperty("displayName").GetString());
        Assert.Equal("34", profile.GetProperty("city").GetString());
        Assert.False(profile.TryGetProperty("email", out _));
    }

    [Fact]
    public async Task Public_profile_of_an_unknown_or_malformed_id_is_rejected()
    {
        var viewer = await TestSession.RegisterAsync(_client);

        await ProblemAssert.IsAsync(
            await viewer.SendAsync(HttpMethod.Get, $"/api/v1/users/{Guid.NewGuid()}"),
            HttpStatusCode.NotFound,
            "not_found");
        await ProblemAssert.IsAsync(
            await viewer.SendAsync(HttpMethod.Get, "/api/v1/users/not-a-uuid"),
            HttpStatusCode.BadRequest,
            "validation_failed");
    }

    [Fact]
    public async Task Avatar_can_be_uploaded_set_replaced_and_deleted()
    {
        var session = await TestSession.RegisterAsync(_client);

        async Task<string> UploadAsync()
        {
            var response = await session.SendAsync(
                HttpMethod.Post,
                "/api/v1/me/avatar/upload-url",
                new { contentType = "image/jpeg", contentLength = 2048 });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            var key = body.GetProperty("objectKey").GetString()!;
            Assert.Contains(key, body.GetProperty("uploadUrl").GetString());
            fixture.Storage.Upload(key, 2048, "image/jpeg");
            return key;
        }

        var first = await UploadAsync();
        var set = await session.SendAsync(HttpMethod.Put, "/api/v1/me/avatar", new { objectKey = first });
        Assert.Equal(HttpStatusCode.OK, set.StatusCode);
        Assert.Contains(first, (await set.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("avatarUrl").GetString());

        var second = await UploadAsync();
        await session.SendAsync(HttpMethod.Put, "/api/v1/me/avatar", new { objectKey = second });
        Assert.False(fixture.Storage.Contains(first));

        var delete = await session.SendAsync(HttpMethod.Delete, "/api/v1/me/avatar");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.False(fixture.Storage.Contains(second));
        var profile = await session.GetJsonAsync("/api/v1/me/profile");
        Assert.Equal(JsonValueKind.Null, profile.GetProperty("avatarUrl").ValueKind);
    }

    [Fact]
    public async Task Avatar_rules_are_enforced()
    {
        var session = await TestSession.RegisterAsync(_client);
        var other = await TestSession.RegisterAsync(_client);

        await ProblemAssert.IsAsync(
            await session.SendAsync(HttpMethod.Post, "/api/v1/me/avatar/upload-url", new { contentType = "image/gif", contentLength = 100 }),
            HttpStatusCode.BadRequest,
            "validation_failed");
        await ProblemAssert.IsAsync(
            await session.SendAsync(HttpMethod.Post, "/api/v1/me/avatar/upload-url", new { contentType = "image/png", contentLength = 6_000_000 }),
            HttpStatusCode.BadRequest,
            "validation_failed");

        // Another user's file cannot be claimed.
        var foreignKey = $"avatars/{other.UserId:N}/photo.jpg";
        fixture.Storage.Upload(foreignKey, 2048, "image/jpeg");
        await ProblemAssert.IsAsync(
            await session.SendAsync(HttpMethod.Put, "/api/v1/me/avatar", new { objectKey = foreignKey }),
            HttpStatusCode.BadRequest,
            "avatar_not_uploaded");

        // Declaring a small file and uploading a large one does not get past the limit.
        var oversized = $"avatars/{session.UserId:N}/big.jpg";
        fixture.Storage.Upload(oversized, 6_000_000, "image/jpeg");
        await ProblemAssert.IsAsync(
            await session.SendAsync(HttpMethod.Put, "/api/v1/me/avatar", new { objectKey = oversized }),
            HttpStatusCode.BadRequest,
            "avatar_invalid");
        Assert.False(fixture.Storage.Contains(oversized));
    }

    [Fact]
    public async Task Cities_lists_all_81_provinces_without_authentication()
    {
        var cities = await _client.GetFromJsonAsync<JsonElement>("/api/v1/cities");

        Assert.Equal(81, cities.GetArrayLength());
        Assert.Equal("01", cities[0].GetProperty("code").GetString());
        Assert.Equal("Adana", cities[0].GetProperty("name").GetString());
        Assert.Contains(cities.EnumerateArray(), city => city.GetProperty("name").GetString() == "İstanbul");
    }
}
