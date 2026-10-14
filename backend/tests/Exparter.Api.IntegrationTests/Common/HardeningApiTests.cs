using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Exparter.Api.IntegrationTests.Common;

/// <summary>The real limits, set low enough to reach in a test.</summary>
public sealed class StrictRateLimitFixture : ApiFixture
{
    public const int UserPerMinute = 15;
    public const int CredentialsPerMinute = 3;
    public const int EmailsPerQuarterHour = 2;

    protected override void ConfigureRateLimits(IWebHostBuilder builder)
    {
        builder.UseSetting("RateLimits:UserPerMinute", UserPerMinute.ToString());
        builder.UseSetting("RateLimits:AnonymousPerMinute", "1000");
        builder.UseSetting("RateLimits:CredentialsPerMinute", CredentialsPerMinute.ToString());
        builder.UseSetting("RateLimits:EmailsPerQuarterHour", EmailsPerQuarterHour.ToString());

        // The test server has no client address; each test plays a client of its own.
        builder.ConfigureServices(services => services.AddSingleton<IStartupFilter, ClientAddressFilter>());
    }

    /// <summary>A client whose requests come from an address of its own.</summary>
    public HttpClient CreateClientAt(string address)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(ClientAddressFilter.Header, address);
        return client;
    }

    private sealed class ClientAddressFilter : IStartupFilter
    {
        public const string Header = "X-Test-Client-Address";

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                if (context.Request.Headers.TryGetValue(Header, out var address))
                {
                    context.Connection.RemoteIpAddress = IPAddress.Parse(address!);
                }

                return nextMiddleware(context);
            });
            next(app);
        };
    }
}

public class HardeningApiTests(StrictRateLimitFixture fixture) : IClassFixture<StrictRateLimitFixture>
{
    private static int _lastAddress;

    // Each test from its own address, so that one test's attempts do not use up another's.
    private readonly HttpClient _client = fixture.CreateClientAt($"10.0.0.{Interlocked.Increment(ref _lastAddress)}");

    [Fact]
    public async Task Guessing_passwords_is_slowed_down_and_the_client_is_told_when_to_retry()
    {
        var login = new { identifier = "nobody", password = "wrong-password" };
        for (var attempt = 0; attempt < StrictRateLimitFixture.CredentialsPerMinute; attempt++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PostAsJsonAsync("/api/v1/auth/login", login)).StatusCode);
        }

        var refused = await _client.PostAsJsonAsync("/api/v1/auth/login", login);

        await ProblemAssert.IsAsync(refused, HttpStatusCode.TooManyRequests, "rate_limited");
        Assert.True(int.Parse(refused.Headers.GetValues("Retry-After").Single()) > 0);
    }

    [Fact]
    public async Task Emails_cannot_be_requested_over_and_over()
    {
        var request = new { email = $"{Guid.NewGuid():N}@example.com" };
        for (var attempt = 0; attempt < StrictRateLimitFixture.EmailsPerQuarterHour; attempt++)
        {
            Assert.Equal(HttpStatusCode.Accepted, (await _client.PostAsJsonAsync("/api/v1/auth/password-reset/request", request)).StatusCode);
        }

        await ProblemAssert.IsAsync(
            await _client.PostAsJsonAsync("/api/v1/auth/password-reset/request", request),
            HttpStatusCode.TooManyRequests,
            "rate_limited");
    }

    [Fact]
    public async Task A_signed_in_user_has_a_budget_of_requests_of_their_own()
    {
        var first = await TestSession.RegisterAsync(_client);
        var second = await TestSession.RegisterAsync(_client);
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < StrictRateLimitFixture.UserPerMinute + 1; i++)
        {
            statuses.Add((await first.SendAsync(HttpMethod.Get, "/api/v1/me")).StatusCode);
        }

        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
        Assert.Equal(HttpStatusCode.OK, (await second.SendAsync(HttpMethod.Get, "/api/v1/me")).StatusCode);
    }

    [Fact]
    public async Task Responses_tell_browsers_not_to_interpret_frame_or_keep_them()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Contains("frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.True(response.Headers.CacheControl!.NoStore);
    }

    [Fact]
    public async Task An_oversized_body_is_refused_before_it_is_read()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/password-reset/confirm",
            new { token = new string('a', 70_000), newPassword = "Passw0rd!x" });

        await ProblemAssert.IsAsync(response, HttpStatusCode.RequestEntityTooLarge, "payload_too_large");
    }
}
