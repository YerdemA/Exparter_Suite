using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Exparter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Exparter.Api.IntegrationTests.Common;

/// <summary>Counts the SQL commands the API sends, so that tests can see how a request scales.</summary>
public sealed class QueryCounter : DbCommandInterceptor
{
    private int _count;

    public int Count => _count;

    public void Reset() => Interlocked.Exchange(ref _count, 0);

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _count);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _count);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _count);
        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }
}

public sealed class QueryCountingFixture : ApiFixture
{
    public QueryCounter Queries { get; } = new();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(Queries));

        // The deadline worker and the push sender query on their own schedule; counted with
        // a request, they would look like part of it.
        foreach (var worker in services
                     .Where(service => service.ServiceType == typeof(IHostedService)
                         && service.ImplementationType?.Name is "MatchingDeadlineWorker" or "PushWorker")
                     .ToList())
        {
            services.Remove(worker);
        }
    }
}

/// <summary>
/// A list must cost the same number of queries whatever its length; one query per item
/// (N+1) shows up as a count that grows with the list.
/// </summary>
public class QueryCountTests(QueryCountingFixture fixture) : IClassFixture<QueryCountingFixture>
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

    /// <summary>A partner the viewer shares everything with: a match, messages, a completed meeting, reviews, notifications.</summary>
    private async Task AddPartnerAsync(TestSession viewer, TestSession pending)
    {
        var partner = await TestSession.RegisterVisibleAsync(_client, fixture);
        var guitar = await partner.FindSkillIdAsync("gitar");
        var request = await OkAsync(
            PostAsync(partner, "/api/v1/swap-requests", new { recipientId = viewer.UserId, offeredSkillId = guitar, requestedSkillId = guitar }),
            HttpStatusCode.Created);
        var match = await OkAsync(PostAsync(viewer, $"/api/v1/swap-requests/{request.GetProperty("id").GetGuid()}/accept"));
        var matchPath = $"/api/v1/matches/{match.GetProperty("id").GetGuid()}";
        await OkAsync(
            PostAsync(partner, $"/api/v1/conversations/{match.GetProperty("conversationId").GetGuid()}/messages", new { body = "Merhaba" }),
            HttpStatusCode.Created);
        var meeting = await OkAsync(PostAsync(partner, $"{matchPath}/sessions/in-person"), HttpStatusCode.Created);
        await OkAsync(PostAsync(viewer, $"/api/v1/sessions/{meeting.GetProperty("id").GetGuid()}/completion/confirm"));
        await OkAsync(
            PostAsync(partner, $"{matchPath}/sessions", new { scheduledAt = DateTimeOffset.UtcNow.AddDays(3), durationMinutes = 60 }),
            HttpStatusCode.Created);
        await OkAsync(PostAsync(partner, "/api/v1/reviews", new { revieweeId = viewer.UserId, rating = 9 }), HttpStatusCode.Created);
        await OkAsync(PostAsync(viewer, "/api/v1/blocks", new { userId = (await TestSession.RegisterAsync(_client)).UserId }), HttpStatusCode.Created);

        // And one more request the viewer has not answered yet.
        var other = await TestSession.RegisterVisibleAsync(_client, fixture);
        await OkAsync(
            PostAsync(other, "/api/v1/swap-requests", new { recipientId = pending.UserId, offeredSkillId = guitar, requestedSkillId = guitar }),
            HttpStatusCode.Created);
    }

    private async Task<Dictionary<string, int>> CountAsync(TestSession viewer, TestSession pending)
    {
        string[] viewerPaths =
        [
            "/api/v1/matches",
            "/api/v1/conversations",
            "/api/v1/notifications",
            $"/api/v1/users/{viewer.UserId}/reviews",
            "/api/v1/sessions",
            "/api/v1/blocks",
            "/api/v1/discovery/users",
            $"/api/v1/users/{viewer.UserId}/stats",
        ];
        var counts = new Dictionary<string, int>();
        foreach (var (user, path) in viewerPaths.Select(path => (viewer, path)).Append((pending, "/api/v1/swap-requests?direction=Incoming")))
        {
            fixture.Queries.Reset();
            await OkAsync(user.SendAsync(HttpMethod.Get, path));
            counts[path.Split('?')[0].Replace(viewer.UserId.ToString(), "{id}")] = fixture.Queries.Count;
        }

        return counts;
    }

    [Fact]
    public async Task The_api_connects_without_jit_compilation()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var setting = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database
            .SqlQuery<string>($"SELECT current_setting('jit') AS \"Value\"")
            .SingleAsync();

        Assert.Equal("off", setting);
    }

    [Fact]
    public async Task Lists_cost_the_same_number_of_queries_however_long_they_are()
    {
        var viewer = await TestSession.RegisterVisibleAsync(_client, fixture);
        var pending = await TestSession.RegisterVisibleAsync(_client, fixture);
        await AddPartnerAsync(viewer, pending);
        var withOne = await CountAsync(viewer, pending);

        // Three is as many matches as a user can have at once.
        for (var i = 0; i < 2; i++)
        {
            await AddPartnerAsync(viewer, pending);
        }

        var withThree = await CountAsync(viewer, pending);

        // Compared as text, so that a failure names every list and its two counts.
        static string Describe(Dictionary<string, int> counts) => string.Join(", ", counts.Select(item => $"{item.Key}={item.Value}"));
        Assert.True(Describe(withOne) == Describe(withThree), $"With one: {Describe(withOne)} · With three: {Describe(withThree)}");
    }
}
