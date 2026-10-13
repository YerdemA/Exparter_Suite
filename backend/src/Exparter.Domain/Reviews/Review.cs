using Exparter.Domain.Common;

namespace Exparter.Domain.Reviews;

public static class ReviewErrors
{
    public const string InvalidReviewCode = "invalid_review";
    public const string ReviewOfSelfCode = "review_of_self";

    public static DomainException InvalidReview(string message) => new(InvalidReviewCode, message);

    public static DomainException ReviewOfSelf() => new(ReviewOfSelfCode, "A user cannot review themselves.");

    public static DomainException NotAuthor() =>
        new(DomainErrorCodes.NotParticipant, "Only its author can delete a review.");
}

/// <summary>
/// What one user thinks of another after a session with them: a score and, optionally,
/// a comment. It is written once and never edited; the author can only take it back.
/// </summary>
public sealed class Review
{
    public const int MinRating = 1;
    public const int MaxRating = 10;
    public const int CommentMaxLength = 500;

    private Review()
    {
    }

    public Guid Id { get; private set; }
    public Guid ReviewerId { get; private set; }
    public Guid RevieweeId { get; private set; }

    /// <summary>The completed session that gave the right to review.</summary>
    public Guid SwapSessionId { get; private set; }

    public int Rating { get; private set; }
    public string? Comment { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// A deleted review stays on record: it no longer shows or counts, but it still
    /// stands in the way of a second review of the same user.
    /// </summary>
    public DateTimeOffset? DeletedAt { get; private set; }

    public bool IsDeleted => DeletedAt is not null;

    public static Review Create(Guid reviewerId, Guid revieweeId, Guid swapSessionId, int rating, string? comment, DateTimeOffset now)
    {
        if (reviewerId == revieweeId)
        {
            throw ReviewErrors.ReviewOfSelf();
        }

        if (rating is < MinRating or > MaxRating)
        {
            throw ReviewErrors.InvalidReview($"The rating must be between {MinRating} and {MaxRating}.");
        }

        var trimmed = comment?.Trim();
        if (trimmed?.Length > CommentMaxLength)
        {
            throw ReviewErrors.InvalidReview($"The comment can be at most {CommentMaxLength} characters.");
        }

        return new Review
        {
            Id = Guid.CreateVersion7(),
            ReviewerId = reviewerId,
            RevieweeId = revieweeId,
            SwapSessionId = swapSessionId,
            Rating = rating,
            Comment = string.IsNullOrEmpty(trimmed) ? null : trimmed,
            CreatedAt = now,
        };
    }

    public void Delete(Guid userId, DateTimeOffset now)
    {
        if (userId != ReviewerId)
        {
            throw ReviewErrors.NotAuthor();
        }

        DeletedAt ??= now;
    }
}
