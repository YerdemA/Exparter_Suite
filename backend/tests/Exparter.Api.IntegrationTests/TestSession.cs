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
            new { email, username, password = Password, ageConfirmed = true });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new TestSession(
            client,
            body.GetProperty("account").GetProperty("id").GetGuid(),
            email,
            username,
            body.GetProperty("accessToken").GetString()!);
    }

    /// <summary>A user other people can see: verified email and a complete profile.</summary>
    public static async Task<TestSession> RegisterVisibleAsync(HttpClient client, ApiFixture fixture)
    {
        var session = await RegisterAsync(client);
        await session.VerifyEmailAsync(fixture);
        var response = await session.SendAsync(
            HttpMethod.Put,
            "/api/v1/me/profile",
            new { displayName = "Test Kullanıcı", bio = "Merhaba", city = "34", acceptsOnline = true, acceptsInPerson = true });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return session;
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
