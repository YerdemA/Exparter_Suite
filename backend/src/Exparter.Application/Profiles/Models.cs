using Exparter.Application.Common;
using Exparter.Domain.Profiles;

namespace Exparter.Application.Profiles;

public interface IProfileRepository
{
    Task<Profile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Profile>> ListByUserIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);

    void Add(Profile profile);
}

public sealed record ProfileDto(
    Guid UserId,
    string? DisplayName,
    string? Bio,
    string? City,
    bool AcceptsOnline,
    bool AcceptsInPerson,
    Uri? AvatarUrl,
    bool IsComplete,
    RatingSummaryDto Rating,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <param name="DisplayName">Null if the user has not filled in their profile yet.</param>
public sealed record PublicProfileDto(
    Guid UserId,
    string Username,
    string? DisplayName,
    string? Bio,
    Uri? AvatarUrl,
    string? City,
    bool AcceptsOnline,
    bool AcceptsInPerson,
    RatingSummaryDto Rating);

public sealed record AvatarUploadUrlDto(Uri UploadUrl, string ObjectKey, DateTimeOffset ExpiresAt);

public sealed record CityDto(string Code, string Name);

internal static class Avatars
{
    public const long MaxBytes = 5 * 1024 * 1024;

    public static readonly TimeSpan UploadUrlLifetime = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan DownloadUrlLifetime = TimeSpan.FromHours(1);

    private static readonly Dictionary<string, string> Extensions = new()
    {
        ["image/jpeg"] = "jpg",
        ["image/png"] = "png",
        ["image/webp"] = "webp",
    };

    public static bool IsAllowedContentType(string? contentType) =>
        contentType is not null && Extensions.ContainsKey(contentType);

    /// <summary>Keys live under the owner's prefix, which is what proves ownership later.</summary>
    public static string PrefixOf(Guid userId) => $"avatars/{userId:N}/";

    public static string NewKey(Guid userId, string contentType) =>
        $"{PrefixOf(userId)}{Guid.CreateVersion7():N}.{Extensions[contentType]}";

    public static Uri? UrlOf(Profile profile, IObjectStorage storage) =>
        profile.AvatarKey is null ? null : storage.CreateDownloadUrl(profile.AvatarKey, DownloadUrlLifetime);
}

internal static class ProfileMapping
{
    public static ProfileDto ToDto(this Profile profile, IObjectStorage storage) =>
        new(
            profile.UserId,
            profile.DisplayName,
            profile.Bio,
            profile.City,
            profile.AcceptsOnline,
            profile.AcceptsInPerson,
            Avatars.UrlOf(profile, storage),
            profile.IsComplete,
            RatingSummaryDto.None,
            profile.CreatedAt,
            profile.UpdatedAt);
}
