using System.Security.Claims;
using Exparter.Api.Common;
using Exparter.Application.Matching;
using Exparter.Domain.Matching;
using Mediator;

namespace Exparter.Api.Matching;

internal sealed record SendSwapRequestRequest(Guid RecipientId, Guid OfferedSkillId, Guid RequestedSkillId, string? Message);

internal static class SwapEndpoints
{
    public static IEndpointRouteBuilder MapSwapEndpoints(this IEndpointRouteBuilder routes)
    {
        var requests = routes.MapGroup("/swap-requests").RequireAuthorization();

        requests.MapGet("", async (SwapRequestDirection direction, SwapRequestStatus? status, int? page, int? pageSize, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new ListSwapRequestsQuery(user.GetUserId(), direction, status, page, pageSize), cancellationToken)));

        requests.MapPost("", async (SendSwapRequestRequest request, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
        {
            var created = await sender.Send(
                new SendSwapRequestCommand(user.GetUserId(), request.RecipientId, request.OfferedSkillId, request.RequestedSkillId, request.Message),
                cancellationToken);
            return Results.Created($"/api/v1/swap-requests/{created.Id}", created);
        });

        requests.MapGet("/{swapRequestId}", async (Guid swapRequestId, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetSwapRequestQuery(user.GetUserId(), swapRequestId), cancellationToken)));

        requests.MapPost("/{swapRequestId}/accept", async (Guid swapRequestId, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new AcceptSwapRequestCommand(user.GetUserId(), swapRequestId), cancellationToken)));

        requests.MapPost("/{swapRequestId}/decline", async (Guid swapRequestId, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new DeclineSwapRequestCommand(user.GetUserId(), swapRequestId), cancellationToken)));

        requests.MapPost("/{swapRequestId}/cancel", async (Guid swapRequestId, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new CancelSwapRequestCommand(user.GetUserId(), swapRequestId), cancellationToken)));

        var matches = routes.MapGroup("/matches").RequireAuthorization();

        matches.MapGet("", async (MatchStatus? status, int? page, int? pageSize, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new ListMatchesQuery(user.GetUserId(), status, page, pageSize), cancellationToken)));

        matches.MapGet("/{matchId}", async (Guid matchId, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetMatchQuery(user.GetUserId(), matchId), cancellationToken)));

        matches.MapPost("/{matchId}/end-request", ChangeEnd(MatchEndAction.Request));
        matches.MapDelete("/{matchId}/end-request", ChangeEnd(MatchEndAction.Withdraw));
        matches.MapPost("/{matchId}/end-request/accept", ChangeEnd(MatchEndAction.Accept));
        matches.MapPost("/{matchId}/end-request/reject", ChangeEnd(MatchEndAction.Reject));

        return routes;
    }

    private static Func<Guid, ClaimsPrincipal, ISender, CancellationToken, Task<IResult>> ChangeEnd(MatchEndAction action) =>
        async (matchId, user, sender, cancellationToken) =>
            Results.Ok(await sender.Send(new ChangeMatchEndCommand(user.GetUserId(), matchId, action), cancellationToken));
}
