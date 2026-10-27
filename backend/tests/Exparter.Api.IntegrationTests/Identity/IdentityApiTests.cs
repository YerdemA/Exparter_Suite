using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Exparter.Api.IntegrationTests.Identity;

[Collection(ApiCollection.Name)]
public class IdentityApiTests(ApiFixture fixture)
{
    private const string Password = "Passw0rd!x";

    private readonly HttpClient _client = fixture.CreateClient();

    private sealed record TestUser(string Email, string Username, string AccessToken, string RefreshToken);

    private static string Unique() => Guid.NewGuid().ToString("N")[..12];

    private async Task<TestUser> RegisterAsync()
    {
        var id = Unique();
        var (email, username) = ($"user{id}@example.com", $"user{id}");
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email, username, password = Password, ageConfirmed = true, termsAccepted = true });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new TestUser(email, username, body.GetProperty("accessToken").GetString()!, body.GetProperty("refreshToken").GetString()!);
    }

    private Task<HttpResponseMessage> LoginAsync(string identifier, string password = Password) =>
        _client.PostAsJsonAsync("/api/v1/auth/login", new { identifier, password });

    private Task<HttpResponseMessage> RefreshAsync(string refreshToken) =>
        _client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken });

    private async Task<HttpResponseMessage> SendAuthorizedAsync(HttpMethod method, string path, string accessToken, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await _client.SendAsync(request);
    }

    private static async Task<JsonElement> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(code, body.GetProperty("code").GetString());
        Assert.Equal((int)status, body.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrEmpty(body.GetProperty("type").GetString()));
        return body;
    }

    [Fact]
    public async Task Health_endpoint_reports_healthy()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Register_creates_an_account_and_signs_in()
    {
        var id = Unique();
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email = $"New{id}@Example.com", username = $"new{id}", password = Password, ageConfirmed = true, termsAccepted = true });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var account = body.GetProperty("account");
        Assert.Equal($"new{id}@example.com", account.GetProperty("email").GetString());
        Assert.Equal($"new{id}", account.GetProperty("username").GetString());
        Assert.False(account.GetProperty("emailVerified").GetBoolean());
        Assert.Equal("User", account.GetProperty("role").GetString());
        Assert.False(body.GetProperty("accountRestored").GetBoolean());
        Assert.Equal(900, body.GetProperty("expiresIn").GetInt32());
        Assert.NotNull(fixture.Emails.LatestVerificationToken($"new{id}@example.com"));
    }

    [Fact]
    public async Task Register_reports_validation_errors_per_field()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email = "not-an-email", username = "a", password = "short", ageConfirmed = false, termsAccepted = false });

        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation_failed");
        var errors = problem.GetProperty("errors");
        Assert.Equal(
            ["ageConfirmed", "email", "password", "termsAccepted", "username"],
            errors.EnumerateObject().Select(property => property.Name).Order());
    }

    [Fact]
    public async Task Register_rejects_malformed_json()
    {
        using var content = new StringContent("{oops", System.Text.Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/v1/auth/register", content);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation_failed");
    }

    [Fact]
    public async Task Unknown_route_returns_problem_details()
    {
        var response = await _client.GetAsync("/api/v1/does-not-exist");

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    public async Task Register_rejects_duplicate_email_and_username()
    {
        var user = await RegisterAsync();

        var sameEmail = await _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email = user.Email.ToUpperInvariant(), username = $"other{Unique()}", password = Password, ageConfirmed = true, termsAccepted = true });
        var sameUsername = await _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email = $"other{Unique()}@example.com", username = user.Username, password = Password, ageConfirmed = true, termsAccepted = true });
        var reserved = await _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email = $"other{Unique()}@example.com", username = "admin", password = Password, ageConfirmed = true, termsAccepted = true });

        await AssertProblemAsync(sameEmail, HttpStatusCode.Conflict, "email_already_registered");
        await AssertProblemAsync(sameUsername, HttpStatusCode.Conflict, "username_already_taken");
        await AssertProblemAsync(reserved, HttpStatusCode.Conflict, "username_reserved");
    }

    [Fact]
    public async Task Register_rejects_an_uppercase_username()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email = $"upper{Unique()}@example.com", username = $"Upper{Unique()}", password = Password, ageConfirmed = true, termsAccepted = true });

        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation_failed");
        Assert.True(problem.GetProperty("errors").TryGetProperty("username", out _));
    }

    [Fact]
    public async Task Username_can_be_changed_and_used_to_sign_in()
    {
        var user = await RegisterAsync();
        var other = await RegisterAsync();
        var newUsername = $"renamed.{Unique()}";

        var change = await SendAuthorizedAsync(HttpMethod.Put, "/api/v1/me/username", user.AccessToken, new { username = newUsername });
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);
        Assert.Equal(newUsername, (await change.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("username").GetString());

        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(newUsername)).StatusCode);
        await AssertProblemAsync(await LoginAsync(user.Username), HttpStatusCode.Unauthorized, "invalid_credentials");

        await AssertProblemAsync(
            await SendAuthorizedAsync(HttpMethod.Put, "/api/v1/me/username", user.AccessToken, new { username = other.Username }),
            HttpStatusCode.Conflict,
            "username_already_taken");
        await AssertProblemAsync(
            await SendAuthorizedAsync(HttpMethod.Put, "/api/v1/me/username", user.AccessToken, new { username = "Not.Lowercase" }),
            HttpStatusCode.BadRequest,
            "validation_failed");
        await AssertProblemAsync(
            await SendAuthorizedAsync(HttpMethod.Put, "/api/v1/me/username", user.AccessToken, new { username = $"again.{Unique()}" }),
            HttpStatusCode.Conflict,
            "username_change_too_soon");
        await AssertProblemAsync(
            await _client.PutAsJsonAsync("/api/v1/me/username", new { username = newUsername }),
            HttpStatusCode.Unauthorized,
            "unauthorized");
    }

    [Fact]
    public async Task Login_works_with_email_and_with_username()
    {
        var user = await RegisterAsync();

        var byEmail = await LoginAsync(user.Email);
        var byUsername = await LoginAsync(user.Username.ToUpperInvariant());

        Assert.Equal(HttpStatusCode.OK, byEmail.StatusCode);
        Assert.Equal(HttpStatusCode.OK, byUsername.StatusCode);
        var body = await byUsername.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(user.Username, body.GetProperty("account").GetProperty("username").GetString());
    }

    [Fact]
    public async Task Login_rejects_wrong_credentials()
    {
        var user = await RegisterAsync();

        await AssertProblemAsync(await LoginAsync(user.Username, "wrong-password"), HttpStatusCode.Unauthorized, "invalid_credentials");
        await AssertProblemAsync(await LoginAsync($"nobody{Unique()}"), HttpStatusCode.Unauthorized, "invalid_credentials");
    }

    [Fact]
    public async Task Me_returns_the_account_for_a_valid_token()
    {
        var user = await RegisterAsync();

        var response = await SendAuthorizedAsync(HttpMethod.Get, "/api/v1/me", user.AccessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(user.Email, body.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Me_requires_a_valid_token()
    {
        await AssertProblemAsync(await _client.GetAsync("/api/v1/me"), HttpStatusCode.Unauthorized, "unauthorized");
        await AssertProblemAsync(
            await SendAuthorizedAsync(HttpMethod.Get, "/api/v1/me", "not-a-jwt"),
            HttpStatusCode.Unauthorized,
            "unauthorized");
    }

    [Fact]
    public async Task Refresh_rotates_the_token_and_detects_reuse()
    {
        var user = await RegisterAsync();

        var first = await RefreshAsync(user.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var rotated = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("refreshToken").GetString()!;
        Assert.NotEqual(user.RefreshToken, rotated);

        // Replaying the old token is treated as theft: it fails and kills its successor too.
        await AssertProblemAsync(await RefreshAsync(user.RefreshToken), HttpStatusCode.Unauthorized, "refresh_token_invalid");
        await AssertProblemAsync(await RefreshAsync(rotated), HttpStatusCode.Unauthorized, "refresh_token_invalid");
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token()
    {
        var user = await RegisterAsync();

        var response = await _client.PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = user.RefreshToken });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await AssertProblemAsync(await RefreshAsync(user.RefreshToken), HttpStatusCode.Unauthorized, "refresh_token_invalid");
    }

    [Fact]
    public async Task Email_verification_flow_verifies_the_address_once()
    {
        var user = await RegisterAsync();
        var registrationToken = fixture.Emails.LatestVerificationToken(user.Email);

        var request = await SendAuthorizedAsync(HttpMethod.Post, "/api/v1/auth/email-verification/request", user.AccessToken);
        Assert.Equal(HttpStatusCode.Accepted, request.StatusCode);
        var newToken = fixture.Emails.LatestVerificationToken(user.Email);
        Assert.NotEqual(registrationToken, newToken);

        // Asking for a new email invalidates the earlier link.
        await AssertProblemAsync(
            await _client.PostAsJsonAsync("/api/v1/auth/email-verification/confirm", new { token = registrationToken }),
            HttpStatusCode.BadRequest,
            "invalid_token");

        var confirm = await _client.PostAsJsonAsync("/api/v1/auth/email-verification/confirm", new { token = newToken });
        Assert.Equal(HttpStatusCode.NoContent, confirm.StatusCode);

        var me = await SendAuthorizedAsync(HttpMethod.Get, "/api/v1/me", user.AccessToken);
        Assert.True((await me.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("emailVerified").GetBoolean());

        await AssertProblemAsync(
            await _client.PostAsJsonAsync("/api/v1/auth/email-verification/confirm", new { token = newToken }),
            HttpStatusCode.BadRequest,
            "invalid_token");
        await AssertProblemAsync(
            await SendAuthorizedAsync(HttpMethod.Post, "/api/v1/auth/email-verification/request", user.AccessToken),
            HttpStatusCode.Conflict,
            "email_already_verified");
    }

    [Fact]
    public async Task Email_verification_request_requires_authentication()
    {
        var response = await _client.PostAsync("/api/v1/auth/email-verification/request", content: null);

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "unauthorized");
    }

    [Fact]
    public async Task Password_reset_flow_changes_the_password_and_signs_out_devices()
    {
        var user = await RegisterAsync();
        const string newPassword = "N3w-Passw0rd";

        var request = await _client.PostAsJsonAsync("/api/v1/auth/password-reset/request", new { email = user.Email });
        Assert.Equal(HttpStatusCode.Accepted, request.StatusCode);
        var token = fixture.Emails.LatestPasswordResetToken(user.Email)!;

        var confirm = await _client.PostAsJsonAsync(
            "/api/v1/auth/password-reset/confirm",
            new { token, newPassword });
        Assert.Equal(HttpStatusCode.NoContent, confirm.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(user.Email, newPassword)).StatusCode);
        await AssertProblemAsync(await LoginAsync(user.Email), HttpStatusCode.Unauthorized, "invalid_credentials");
        await AssertProblemAsync(await RefreshAsync(user.RefreshToken), HttpStatusCode.Unauthorized, "refresh_token_invalid");
        await AssertProblemAsync(
            await _client.PostAsJsonAsync("/api/v1/auth/password-reset/confirm", new { token, newPassword }),
            HttpStatusCode.BadRequest,
            "invalid_token");
    }

    [Fact]
    public async Task Password_reset_request_does_not_reveal_unknown_emails()
    {
        var email = $"nobody{Unique()}@example.com";

        var response = await _client.PostAsJsonAsync("/api/v1/auth/password-reset/request", new { email });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Null(fixture.Emails.LatestPasswordResetToken(email));
    }

    [Fact]
    public async Task Account_deletion_can_be_undone_by_signing_in()
    {
        var user = await RegisterAsync();

        await AssertProblemAsync(
            await SendAuthorizedAsync(HttpMethod.Post, "/api/v1/me/deletion-request", user.AccessToken, new { password = "wrong-password" }),
            HttpStatusCode.BadRequest,
            "invalid_password");

        var deletion = await SendAuthorizedAsync(
            HttpMethod.Post,
            "/api/v1/me/deletion-request",
            user.AccessToken,
            new { password = Password });
        Assert.Equal(HttpStatusCode.Accepted, deletion.StatusCode);
        var permanentDeletionAt = (await deletion.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("permanentDeletionAt").GetDateTimeOffset();
        Assert.InRange(permanentDeletionAt, DateTimeOffset.UtcNow.AddDays(29), DateTimeOffset.UtcNow.AddDays(31));

        await AssertProblemAsync(await RefreshAsync(user.RefreshToken), HttpStatusCode.Unauthorized, "refresh_token_invalid");

        var login = await LoginAsync(user.Username);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.True((await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accountRestored").GetBoolean());
    }
}
