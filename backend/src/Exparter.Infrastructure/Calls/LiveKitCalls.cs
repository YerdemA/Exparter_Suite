using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Exparter.Application.Sessions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Exparter.Infrastructure.Calls;

public sealed class LiveKitOptions
{
    public const string SectionName = "LiveKit";

    /// <summary>The WebSocket address clients connect to.</summary>
    [Required]
    public string Url { get; init; } = null!;

    /// <summary>The HTTP address the API manages rooms through; inside the server's network, not the public one.</summary>
    [Required]
    public string ApiUrl { get; init; } = null!;

    [Required]
    public string ApiKey { get; init; } = null!;

    /// <summary>HS256 needs at least 256 bits of key material.</summary>
    [Required]
    [MinLength(32)]
    public string ApiSecret { get; init; } = null!;
}

/// <summary>
/// LiveKit tokens are JWTs signed with the API secret, and its room API is Twirp (JSON
/// over HTTP POST); both are small enough that its server SDK is not needed.
/// </summary>
internal sealed class LiveKitCalls(IOptions<LiveKitOptions> options, TimeProvider timeProvider)
    : ICallTokenIssuer, ICallRooms, IDisposable
{
    /// <summary>
    /// What a participant may send. The shared screen goes without the device's sound, so
    /// that notifications and other apps are not heard by the other user. Data messages
    /// carry the public keys with which the two devices agree on the call's encryption key
    /// (ADR 0016).
    /// </summary>
    private static readonly string[] PublishSources = ["camera", "microphone", "screen_share"];

    private static readonly TimeSpan ServerTokenLifetime = TimeSpan.FromMinutes(1);

    private readonly LiveKitOptions _options = options.Value;
    private readonly JsonWebTokenHandler _handler = new();
    private readonly HttpClient _http = new(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) })
    {
        Timeout = TimeSpan.FromSeconds(10),
    };

    public CallTokenDto Issue(string room, Guid userId, DateTimeOffset expiresAt)
    {
        var token = Sign(
            new Dictionary<string, object>
            {
                ["room"] = room,
                ["roomJoin"] = true,
                ["canPublish"] = true,
                ["canSubscribe"] = true,
                ["canPublishData"] = true,
                ["canPublishSources"] = PublishSources,
            },
            expiresAt,
            userId.ToString());
        return new CallTokenDto(_options.Url, token, expiresAt);
    }

    public async Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken)
    {
        using var response = await PostAsync("ListRooms", new { }, cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ListRoomsResponse>(cancellationToken);
        return body?.Rooms?.Select(room => room.Name).ToList() ?? [];
    }

    public async Task CloseAsync(string room, CancellationToken cancellationToken)
    {
        using var response = await PostAsync("DeleteRoom", new { room }, cancellationToken);
        if (response.StatusCode != HttpStatusCode.NotFound)
        {
            response.EnsureSuccessStatusCode();
        }
    }

    public void Dispose() => _http.Dispose();

    /// <summary>A short-lived token for the API's own calls to the room service.</summary>
    internal string CreateServerToken() =>
        Sign(
            new Dictionary<string, object> { ["roomList"] = true, ["roomCreate"] = true },
            timeProvider.GetUtcNow() + ServerTokenLifetime,
            subject: null);

    private Task<HttpResponseMessage> PostAsync(string method, object body, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.ApiUrl.TrimEnd('/')}/twirp/livekit.RoomService/{method}")
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateServerToken());
        return _http.SendAsync(request, cancellationToken);
    }

    private string Sign(Dictionary<string, object> videoGrant, DateTimeOffset expiresAt, string? subject)
    {
        var now = timeProvider.GetUtcNow();
        return _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _options.ApiKey,
            Subject = subject is null ? null : new ClaimsIdentity([new Claim("sub", subject)]),
            NotBefore = now.UtcDateTime,
            IssuedAt = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            Claims = new Dictionary<string, object> { ["video"] = videoGrant },
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.ApiSecret)),
                SecurityAlgorithms.HmacSha256),
        });
    }

    private sealed record ListRoomsResponse(IReadOnlyList<RoomInfo>? Rooms);

    private sealed record RoomInfo(string Name);
}
