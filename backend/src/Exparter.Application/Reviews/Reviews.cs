using Exparter.Application.Common;
using Exparter.Application.Identity;
using Exparter.Application.Moderation;
using Exparter.Application.Notifications;
using Exparter.Application.Sessions;
using Exparter.Domain.Notifications;
using Exparter.Domain.Reviews;
using FluentValidation;
using Mediator;

namespace Exparter.Application.Reviews;

public interface IReviewRepository
{
    /// <summary>Null for an unknown id and for a deleted review.</summary>
    Task<Review?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Whether the reviewer has ever reviewed the user, deleted reviews included.</summary>
    Task<bool> ExistsAsync(Guid reviewerId, Guid revieweeId, CancellationToken cancellationToken);

    /// <summary>Reviews that are not deleted, newest first.</summary>
    Task<PagedList<Review>> ListReceivedAsync(Guid revieweeId, PageRequest page, CancellationToken cancellationToken);

    /// <inheritdoc cref="ListReceivedAsync"/>
    Task<PagedList<Review>> ListGivenAsync(Guid reviewerId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>Figures over reviews that are not deleted. Users without any are left out.</summary>
    Task<IReadOnlyDictionary<Guid, RatingSummaryDto>> GetRatingsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken);

    /// <summary>Removes, for good, every review the user has received.</summary>
    Task DeleteReceivedAsync(Guid revieweeId, CancellationToken cancellationToken);

    void Add(Review review);
}

/// <summary>What other modules may know about reviews.</summary>
/// <summary>A review as a report sees it.</summary>
public sealed record ReviewInfo(Guid Id, Guid ReviewerId, int Rating, string? Comment);

public interface IReviewDirectory
{
    /// <returns><see cref="RatingSummaryDto.None"/> for a user without reviews.</returns>
    Task<RatingSummaryDto> GetRatingAsync(Guid userId, CancellationToken cancellationToken);

    /// <returns>Null for an unknown or deleted review.</returns>
    Task<ReviewInfo?> FindAsync(Guid reviewId, CancellationToken cancellationToken);
}

internal sealed class ReviewDirectory(IReviewRepository reviews) : IReviewDirectory
{
    public async Task<ReviewInfo?> FindAsync(Guid reviewId, CancellationToken cancellationToken) =>
        await reviews.GetByIdAsync(reviewId, cancellationToken) is { IsDeleted: false } review
            ? new ReviewInfo(review.Id, review.ReviewerId, review.Rating, review.Comment)
            : null;

    public async Task<RatingSummaryDto> GetRatingAsync(Guid userId, CancellationToken cancellationToken) =>
        (await reviews.GetRatingsAsync([userId], cancellationToken)).GetValueOrDefault(userId, RatingSummaryDto.None);
}

public sealed record ReviewDto(
    Guid Id,
    UserSummaryDto Reviewer,
    Guid RevieweeId,
    Guid SessionId,
    int Rating,
    string? Comment,
    DateTimeOffset CreatedAt);

public sealed class ReviewDtoFactory(IUserSummaries summaries)
{
    public async Task<ReviewDto> CreateAsync(Review review, CancellationToken cancellationToken) =>
        (await CreateAsync([review], cancellationToken))[0];

    public async Task<IReadOnlyList<ReviewDto>> CreateAsync(IReadOnlyList<Review> reviews, CancellationToken cancellationToken)
    {
        var reviewers = await summaries.GetAsync(reviews.Select(review => review.ReviewerId).ToList(), cancellationToken);
        return reviews
            .Select(review => new ReviewDto(
                review.Id,
                reviewers[review.ReviewerId],
                review.RevieweeId,
                review.SwapSessionId,
                review.Rating,
                review.Comment,
                review.CreatedAt))
            .ToList();
    }

    public async Task<PagedList<ReviewDto>> CreateAsync(PagedList<Review> page, CancellationToken cancellationToken) =>
        new(await CreateAsync(page.Items, cancellationToken), page.Page, page.PageSize, page.TotalCount);
}

internal static class ReviewExtensions
{
    /// <summary>Suspended and leaving accounts answer like an unknown id.</summary>
    public static async Task EnsureActiveAsync(this IUserDirectory users, Guid userId, CancellationToken cancellationToken)
    {
        if (await users.FindAsync(userId, cancellationToken) is not { IsActive: true })
        {
            throw AppException.NotFound("The user was not found.");
        }
    }
}

public sealed record CreateReviewCommand(Guid UserId, Guid RevieweeId, int Rating, string? Comment) : ICommand<ReviewDto>;

public sealed class CreateReviewCommandValidator : AbstractValidator<CreateReviewCommand>
{
    public CreateReviewCommandValidator()
    {
        RuleFor(x => x.Rating).InclusiveBetween(Review.MinRating, Review.MaxRating);
        RuleFor(x => x.Comment).MaximumLength(Review.CommentMaxLength);
    }
}

public sealed class CreateReviewCommandHandler(
    IInteractionRequirements requirements,
    IUserDirectory users,
    IBlockDirectory blocks,
    ISessionDirectory sessions,
    IReviewRepository reviews,
    ReviewDtoFactory dtoFactory,
    IUserNotifier notifier,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<CreateReviewCommand, ReviewDto>
{
    public async ValueTask<ReviewDto> Handle(CreateReviewCommand command, CancellationToken cancellationToken)
    {
        await requirements.EnsureCanInteractAsync(command.UserId, cancellationToken);
        if (command.RevieweeId == command.UserId)
        {
            throw ReviewErrors.ReviewOfSelf();
        }

        await users.EnsureActiveAsync(command.RevieweeId, cancellationToken);
        await blocks.EnsureNotBlockedAsync(command.UserId, command.RevieweeId, cancellationToken);

        // A deleted review counts too: taking a review back does not earn a second one.
        if (await reviews.ExistsAsync(command.UserId, command.RevieweeId, cancellationToken))
        {
            throw ReviewConflicts.AlreadyExists();
        }

        var sessionId = await sessions.FindCompletedBetweenAsync(command.UserId, command.RevieweeId, cancellationToken)
            ?? throw AppException.Conflict("session_not_completed", "A review requires a completed session between the two users.");

        var review = Review.Create(command.UserId, command.RevieweeId, sessionId, command.Rating, command.Comment, timeProvider.GetUtcNow());
        reviews.Add(review);
        notifier.Notify(
            review.RevieweeId,
            NotificationType.ReviewReceived,
            new NotificationData(ActorUserId: command.UserId, ReviewId: review.Id));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await dtoFactory.CreateAsync(review, cancellationToken);
    }
}

public static class ReviewConflicts
{
    public static AppException AlreadyExists() =>
        AppException.Conflict("review_already_exists", "The user has already been reviewed by this user.");
}

public sealed record DeleteReviewCommand(Guid UserId, Guid ReviewId) : ICommand;

public sealed class DeleteReviewCommandHandler(IReviewRepository reviews, IUnitOfWork unitOfWork, TimeProvider timeProvider)
    : ICommandHandler<DeleteReviewCommand>
{
    public async ValueTask<Unit> Handle(DeleteReviewCommand command, CancellationToken cancellationToken)
    {
        var review = await reviews.GetByIdAsync(command.ReviewId, cancellationToken)
            ?? throw AppException.NotFound("The review was not found.");
        review.Delete(command.UserId, timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed record ListUserReviewsQuery(Guid UserId, int? Page, int? PageSize) : IQuery<PagedList<ReviewDto>>;

public sealed class ListUserReviewsQueryHandler(IUserDirectory users, IReviewRepository reviews, ReviewDtoFactory dtoFactory)
    : IQueryHandler<ListUserReviewsQuery, PagedList<ReviewDto>>
{
    public async ValueTask<PagedList<ReviewDto>> Handle(ListUserReviewsQuery query, CancellationToken cancellationToken)
    {
        await users.EnsureActiveAsync(query.UserId, cancellationToken);
        return await dtoFactory.CreateAsync(
            await reviews.ListReceivedAsync(query.UserId, PageRequest.From(query.Page, query.PageSize), cancellationToken),
            cancellationToken);
    }
}

public sealed record ListGivenReviewsQuery(Guid UserId, int? Page, int? PageSize) : IQuery<PagedList<ReviewDto>>;

public sealed class ListGivenReviewsQueryHandler(IReviewRepository reviews, ReviewDtoFactory dtoFactory)
    : IQueryHandler<ListGivenReviewsQuery, PagedList<ReviewDto>>
{
    public async ValueTask<PagedList<ReviewDto>> Handle(ListGivenReviewsQuery query, CancellationToken cancellationToken) =>
        await dtoFactory.CreateAsync(
            await reviews.ListGivenAsync(query.UserId, PageRequest.From(query.Page, query.PageSize), cancellationToken),
            cancellationToken);
}

public enum ReviewBlockReason
{
    Self,
    AlreadyReviewed,
    NoCompletedSession,
}

/// <param name="Reason">Null when a review can be written.</param>
public sealed record ReviewEligibilityDto(bool CanReview, ReviewBlockReason? Reason);

/// <summary>Whether <paramref name="UserId"/> can review <paramref name="OtherUserId"/>.</summary>
public sealed record GetReviewEligibilityQuery(Guid UserId, Guid OtherUserId) : IQuery<ReviewEligibilityDto>;

public sealed class GetReviewEligibilityQueryHandler(
    IUserDirectory users,
    IBlockDirectory blocks,
    ISessionDirectory sessions,
    IReviewRepository reviews)
    : IQueryHandler<GetReviewEligibilityQuery, ReviewEligibilityDto>
{
    public async ValueTask<ReviewEligibilityDto> Handle(GetReviewEligibilityQuery query, CancellationToken cancellationToken)
    {
        await users.EnsureActiveAsync(query.OtherUserId, cancellationToken);
        if (query.OtherUserId != query.UserId)
        {
            await blocks.EnsureNotBlockedAsync(query.UserId, query.OtherUserId, cancellationToken);
        }

        ReviewBlockReason? reason =
            query.OtherUserId == query.UserId ? ReviewBlockReason.Self
            : await reviews.ExistsAsync(query.UserId, query.OtherUserId, cancellationToken) ? ReviewBlockReason.AlreadyReviewed
            : await sessions.FindCompletedBetweenAsync(query.UserId, query.OtherUserId, cancellationToken) is null ? ReviewBlockReason.NoCompletedSession
            : null;
        return new ReviewEligibilityDto(reason is null, reason);
    }
}

/// <summary>
/// An account is being erased: what others said about the user goes with it. What the
/// user said about others stays, under the name of a deleted user.
/// </summary>
public sealed class RemoveReviewsOnAccountErased(IReviewRepository reviews) : INotificationHandler<AccountErased>
{
    public async ValueTask Handle(AccountErased notification, CancellationToken cancellationToken) =>
        await reviews.DeleteReceivedAsync(notification.UserId, cancellationToken);
}
