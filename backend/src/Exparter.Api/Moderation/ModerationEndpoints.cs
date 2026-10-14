using System.Security.Claims;
using Exparter.Api.Common;
using Exparter.Application.Identity;
using Exparter.Application.Moderation;
using Exparter.Application.Sessions;
using Exparter.Domain.Moderation;
using Mediator;

namespace Exparter.Api.Moderation;

internal sealed record BlockUserRequest(Guid UserId);

internal sealed record CreateReportRequest(ReportTargetType TargetType, Guid TargetId, ReportReason Reason, string? Description);

internal sealed record ResolveReportRequest(ReportStatus Status);

internal static class ModerationEndpoints
{
    public static IEndpointRouteBuilder MapModerationEndpoints(this IEndpointRouteBuilder routes)
    {
        var blocks = routes.MapGroup("/blocks").RequireAuthorization();

        blocks.MapGet("", async (int? page, int? pageSize, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new ListBlocksQuery(user.GetUserId(), page, pageSize), cancellationToken)));

        blocks.MapPost("", async (BlockUserRequest request, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
        {
            var block = await sender.Send(new BlockUserCommand(user.GetUserId(), request.UserId), cancellationToken);
            return Results.Created("/api/v1/blocks", block);
        });

        blocks.MapDelete("/{userId}", async (Guid userId, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
        {
            await sender.Send(new UnblockUserCommand(user.GetUserId(), userId), cancellationToken);
            return Results.NoContent();
        });

        routes.MapPost("/reports", async (CreateReportRequest request, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
            {
                var report = await sender.Send(
                    new CreateReportCommand(user.GetUserId(), request.TargetType, request.TargetId, request.Reason, request.Description),
                    cancellationToken);
                return Results.Created($"/api/v1/reports/{report.Id}", report);
            })
            .RequireAuthorization();

        var admin = routes.MapGroup("/admin").RequireAuthorization(Authentication.AdminPolicy);

        admin.MapGet("/reports", async (ReportStatus? status, int? page, int? pageSize, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new AdminListReportsQuery(status, page, pageSize), cancellationToken)));

        admin.MapPost("/reports/{reportId}/resolution", async (Guid reportId, ResolveReportRequest request, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new CloseReportCommand(user.GetUserId(), reportId, request.Status), cancellationToken)));

        admin.MapGet("/session-cancellations", async (int? page, int? pageSize, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new AdminListSessionCancellationsQuery(page, pageSize), cancellationToken)));

        admin.MapPost("/users/{userId}/suspension", async (Guid userId, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
        {
            await sender.Send(new SuspendUserCommand(user.GetUserId(), userId), cancellationToken);
            return Results.NoContent();
        });

        admin.MapDelete("/users/{userId}/suspension", async (Guid userId, ISender sender, CancellationToken cancellationToken) =>
        {
            await sender.Send(new LiftSuspensionCommand(userId), cancellationToken);
            return Results.NoContent();
        });

        return routes;
    }
}
