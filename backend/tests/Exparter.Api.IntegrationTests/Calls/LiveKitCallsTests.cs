using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Exparter.Infrastructure.Calls;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Exparter.Api.IntegrationTests.Calls;

/// <summary>
/// Checks the tokens and room API calls against the same media server and version that
/// docker-compose.yml runs, since both are written by hand instead of with its SDK.
/// </summary>
public sealed class LiveKitCallsTests : IAsyncLifetime
{
    private const string ApiKey = "test-key";
    private const string ApiSecret = "livekit-tests-secret-0123456789abcdef";

    private readonly IContainer _livekit = new ContainerBuilder("livekit/livekit-server:v1.13.11")
        .WithCommand("--bind", "0.0.0.0", "--node-ip", "127.0.0.1", "--keys", $"{ApiKey}: {ApiSecret}")
        .WithPortBinding(7880, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(7880).ForPath("/")))
        .Build();

    private readonly HttpClient _http = new();
    private LiveKitCalls _calls = null!;
    private string _apiUrl = null!;

    public async Task InitializeAsync()
    {
        await _livekit.StartAsync();
        _apiUrl = $"http://{_livekit.Hostname}:{_livekit.GetMappedPublicPort(7880)}";
        _calls = Create(ApiSecret);
    }

    public async Task DisposeAsync()
    {
        _calls.Dispose();
        _http.Dispose();
        await _livekit.DisposeAsync();
    }

    private LiveKitCalls Create(string secret) =>
        new(
            Options.Create(new LiveKitOptions { Url = "ws://unused", ApiUrl = _apiUrl, ApiKey = ApiKey, ApiSecret = secret }),
            TimeProvider.System);

    /// <summary>Stands in for someone joining a call, which is when the server opens its room.</summary>
    private async Task CreateRoomAsync(string room)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_apiUrl}/twirp/livekit.RoomService/CreateRoom")
        {
            Content = JsonContent.Create(new { name = room }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _calls.CreateServerToken());
        using var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>The check the client SDKs make before connecting with a token.</summary>
    private async Task<HttpStatusCode> ValidateAsync(string token)
    {
        using var response = await _http.GetAsync($"{_apiUrl}/rtc/validate?access_token={token}");
        return response.StatusCode;
    }

    [Fact]
    public async Task Rooms_are_listed_and_closed()
    {
        Assert.Empty(await _calls.ListAsync(CancellationToken.None));
        await CreateRoomAsync("first");
        await CreateRoomAsync("second");

        Assert.Equal(["first", "second"], (await _calls.ListAsync(CancellationToken.None)).Order());

        await _calls.CloseAsync("first", CancellationToken.None);
        Assert.Equal(["second"], await _calls.ListAsync(CancellationToken.None));

        // The room may have closed by itself already, once everyone left.
        await _calls.CloseAsync("first", CancellationToken.None);
    }

    [Fact]
    public async Task The_server_accepts_the_tokens_it_signed_and_only_those()
    {
        var call = _calls.Issue(Guid.NewGuid().ToString(), Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(30));
        var forged = Create("some-other-secret-0123456789abcdefgh")
            .Issue(Guid.NewGuid().ToString(), Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(30));

        Assert.Equal(HttpStatusCode.OK, await ValidateAsync(call.Token));
        Assert.Equal(HttpStatusCode.Unauthorized, await ValidateAsync(forged.Token));
    }

    [Fact]
    public void A_participant_may_share_the_screen_but_not_the_device_sound_and_may_set_its_public_key()
    {
        var call = _calls.Issue("room-1", Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(30));

        var video = new JsonWebToken(call.Token).GetPayloadValue<JsonElement>("video");
        var sources = video.GetProperty("canPublishSources").EnumerateArray().Select(source => source.GetString());
        Assert.Equal(["camera", "microphone", "screen_share"], sources);
        Assert.False(video.GetProperty("canPublishData").GetBoolean());
        // The public keys of the call's encryption go as participant attributes (ADR 0016).
        Assert.True(video.GetProperty("canUpdateOwnMetadata").GetBoolean());
    }
}
