namespace Exparter.Application.Common;

/// <summary>Placeholder until the Reviews module provides real figures.</summary>
public sealed record RatingSummaryDto(int ReviewCount, double? AverageRating)
{
    public static RatingSummaryDto None { get; } = new(0, null);
}

/// <summary>How a user appears inside another resource, such as a discovery result.</summary>
/// <param name="DisplayName">Null if the user has not filled in their profile, and for deleted users.</param>
public sealed record UserSummaryDto(Guid UserId, string? Username, string? DisplayName, Uri? AvatarUrl, bool IsDeleted);

public sealed record SkillRefDto(Guid Id, string Name);

/// <summary>A cursor-based slice of a list, as described in docs/api-conventions.md.</summary>
/// <param name="NextCursor">Null when there are no more items.</param>
public sealed record CursorPage<T>(IReadOnlyList<T> Items, string? NextCursor);
