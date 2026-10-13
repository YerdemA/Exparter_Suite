using System.Security.Claims;
using Exparter.Api.Common;
using Exparter.Application.Common;
using Exparter.Application.Sessions;
using Exparter.Domain.Sessions;
using Mediator;

namespace Exparter.Api.Sessions;

internal sealed record ProposeSessionRequest(DateTimeOffset ScheduledAt, int DurationMinutes, string? Note);

internal sealed record CancelSessionRequest(string? Reason);

internal static class SessionEndpoints
{
    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder routes)
    {
        var ofMatch = routes.MapGroup("/matches/{matchId}/sessions").RequireAuthorization();

        ofMatch.MapGet("", async (Guid matchId, int? page, int? pageSize, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new ListMatchSessionsQuery(user.GetUserId(), matchId, page, pageSize), cancellationToken)));

        ofMatch.MapPost("", async (Guid matchId, ProposeSessionRequest request, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
        {
            var session = await sender.Send(
                new ProposeSessionCommand(user.GetUserId(), matchId, request.ScheduledAt, request.DurationMinutes, request.Note),
                cancellationToken);
            return Results.Created($"/api/v1/sessions/{session.Id}", session);
        });

        ofMatch.MapPost("/in-person", async (Guid matchId, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
        {
            var session = await sender.Send(new ReportInPersonSessionCommand(user.GetUserId(), matchId), cancellationToken);
            return Results.Created($"/api/v1/sessions/{session.Id}", session);
        });

        var sessions = routes.MapGroup("/sessions").RequireAuthorization();

        sessions.MapGet("", async (SwapSessionStatus? status, DateTimeOffset? from, DateTimeOffset? to, int? page, int? pageSize, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new ListSessionsQuery(user.GetUserId(), status, from, to, page, pageSize), cancellationToken)));

        sessions.MapGet("/{sessionId}", async (Guid sessionId, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetSessionQuery(user.GetUserId(), sessionId), cancellationToken)));

        sessions.MapPost("/{sessionId}/accept", Change(SessionAction.Accept));
        sessions.MapPost("/{sessionId}/decline", Change(SessionAction.Decline));
        sessions.MapPost("/{sessionId}/completion", Change(SessionAction.MarkCompleted));
        sessions.MapPost("/{sessionId}/completion/confirm", Change(SessionAction.ConfirmCompleted));

        sessions.MapPost("/{sessionId}/cancel", async (Guid sessionId, CancelSessionRequest request, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(
                new CancelSessionCommand(user.GetUserId(), sessionId, request.Reason ?? string.Empty),
                cancellationToken)));

        routes.MapGet("/users/{userId}/stats", async (Guid userId, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetUserStatsQuery(userId), cancellationToken)))
            .RequireAuthorization();

        return routes;
    }

    private static Func<Guid, ClaimsPrincipal, ISender, CancellationToken, Task<IResult>> Change(SessionAction action) =>
        async (sessionId, user, sender, cancellationToken) =>
            Results.Ok(await sender.Send(new ChangeSessionCommand(user.GetUserId(), sessionId, action), cancellationToken));
}
