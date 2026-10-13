using System.Security.Claims;
using Exparter.Api.Common;
using Exparter.Application.Reviews;
using Mediator;

namespace Exparter.Api.Reviews;

internal sealed record CreateReviewRequest(Guid RevieweeId, int Rating, string? Comment);

internal static class ReviewEndpoints
{
    public static IEndpointRouteBuilder MapReviewEndpoints(this IEndpointRouteBuilder routes)
    {
        var reviews = routes.MapGroup("/reviews").RequireAuthorization();

        reviews.MapPost("", async (CreateReviewRequest request, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
        {
            var review = await sender.Send(
                new CreateReviewCommand(user.GetUserId(), request.RevieweeId, request.Rating, request.Comment),
                cancellationToken);
            return Results.Created($"/api/v1/users/{review.RevieweeId}/reviews", review);
        });

        reviews.MapDelete("/{reviewId}", async (Guid reviewId, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
        {
            await sender.Send(new DeleteReviewCommand(user.GetUserId(), reviewId), cancellationToken);
            return Results.NoContent();
        });

        routes.MapGet("/users/{userId}/reviews", async (Guid userId, int? page, int? pageSize, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new ListUserReviewsQuery(userId, page, pageSize), cancellationToken)))
            .RequireAuthorization();

        routes.MapGet("/users/{userId}/review-eligibility", async (Guid userId, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetReviewEligibilityQuery(user.GetUserId(), userId), cancellationToken)))
            .RequireAuthorization();

        routes.MapGet("/me/reviews/given", async (int? page, int? pageSize, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new ListGivenReviewsQuery(user.GetUserId(), page, pageSize), cancellationToken)))
            .RequireAuthorization();

        return routes;
    }
}
