using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Exparter.Api.IntegrationTests;

/// <summary>A registered user and the means to call the API as them.</summary>
public sealed class TestSession
{
    public const string Password = "Passw0rd!x";

    private readonly HttpClient _client;

    private TestSession(HttpClient client, Guid userId, string email, string username, string accessToken)
    {
        _client = client;
        UserId = userId;
        Email = email;
        Username = username;
        AccessToken = accessToken;
    }

    public Guid UserId { get; }
    public string Email { get; }
    public string Username { get; }
    public string AccessToken { get; }

    public static async Task<TestSession> RegisterAsync(HttpClient client)
    {
        var id = Guid.NewGuid().ToString("N")[..12];
        var (email, username) = ($"user{id}@example.com", $"user{id}");
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email, username, password = Password, ageConfirmed = true, termsAccepted = true });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new TestSession(
            client,
            body.GetProperty("account").GetProperty("id").GetGuid(),
            email,
            username,
            body.GetProperty("accessToken").GetString()!);
    }

    /// <summary>A user who meets every interaction requirement, and is therefore visible to others.</summary>
    public static async Task<TestSession> RegisterVisibleAsync(HttpClient client, ApiFixture fixture)
    {
        var session = await RegisterAsync(client);
        await session.VerifyEmailAsync(fixture);
        var response = await session.SendAsync(
            HttpMethod.Put,
            "/api/v1/me/profile",
            new { displayName = "Test Kullanıcı", bio = "Merhaba", city = "34", acceptsOnline = true, acceptsInPerson = true });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await session.AddSkillAsync("gitar", "Offered", "Advanced");
        return session;
    }

    /// <summary>Adds a catalog skill, found by searching for <paramref name="search"/>.</summary>
    /// <returns>The id of the new user skill.</returns>
    public async Task<Guid> AddSkillAsync(string search, string type, string level)
    {
        var response = await SendAsync(
            HttpMethod.Post,
            "/api/v1/me/skills",
            new { skillId = await FindSkillIdAsync(search), type, level });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    public async Task<Guid> FindSkillIdAsync(string search)
    {
        var skills = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/skills?q={Uri.EscapeDataString(search)}");
        return skills.GetProperty("items")[0].GetProperty("id").GetGuid();
    }

    /// <summary>Signs in again, for a token that reflects a role changed after registration.</summary>
    public async Task<TestSession> SignInAgainAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = Username, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new TestSession(_client, UserId, Email, Username, body.GetProperty("accessToken").GetString()!);
    }

    public async Task VerifyEmailAsync(ApiFixture fixture)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/email-verification/confirm",
            new { token = fixture.Emails.LatestVerificationToken(Email) });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await _client.SendAsync(request);
    }

    public async Task<JsonElement> GetJsonAsync(string path)
    {
        var response = await SendAsync(HttpMethod.Get, path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}

public static class ProblemAssert
{
    public static async Task<JsonElement> IsAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(code, body.GetProperty("code").GetString());
        return body;
    }
}
