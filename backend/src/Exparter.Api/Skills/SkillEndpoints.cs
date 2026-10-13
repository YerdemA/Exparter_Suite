using System.Security.Claims;
using Exparter.Api.Common;
using Exparter.Application.Skills;
using Exparter.Domain.Skills;
using Mediator;

namespace Exparter.Api.Skills;

internal sealed record ProposeSkillRequest(Guid CategoryId, string Name);

internal sealed record AddUserSkillRequest(Guid SkillId, UserSkillType Type, SkillLevel Level);

internal sealed record UpdateUserSkillRequest(SkillLevel Level);

internal static class SkillEndpoints
{
    public static IEndpointRouteBuilder MapSkillEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/skill-categories", async (ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new ListSkillCategoriesQuery(), cancellationToken)));

        routes.MapGet("/skills", async (Guid? categoryId, string? q, int? page, int? pageSize, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new ListSkillsQuery(categoryId, q, page, pageSize), cancellationToken)));

        routes.MapPost("/skill-proposals", async (ProposeSkillRequest request, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
        {
            var skill = await sender.Send(new ProposeSkillCommand(user.GetUserId(), request.CategoryId, request.Name), cancellationToken);
            return Results.Created("/api/v1/me/skill-proposals", skill);
        }).RequireAuthorization();

        var me = routes.MapGroup("/me").RequireAuthorization();

        me.MapGet("/skill-proposals", async (int? page, int? pageSize, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new ListOwnSkillProposalsQuery(user.GetUserId(), page, pageSize), cancellationToken)));

        me.MapGet("/skills", async (UserSkillType? type, int? page, int? pageSize, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new ListOwnSkillsQuery(user.GetUserId(), type, page, pageSize), cancellationToken)));

        me.MapPost("/skills", async (AddUserSkillRequest request, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
        {
            var userSkill = await sender.Send(
                new AddOwnSkillCommand(user.GetUserId(), request.SkillId, request.Type, request.Level),
                cancellationToken);
            return Results.Created($"/api/v1/me/skills/{userSkill.Id}", userSkill);
        });

        me.MapPatch("/skills/{userSkillId}", async (Guid userSkillId, UpdateUserSkillRequest request, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new UpdateOwnSkillCommand(user.GetUserId(), userSkillId, request.Level), cancellationToken)));

        me.MapDelete("/skills/{userSkillId}", async (Guid userSkillId, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
        {
            await sender.Send(new RemoveOwnSkillCommand(user.GetUserId(), userSkillId), cancellationToken);
            return Results.NoContent();
        });

        routes.MapGet("/users/{userId}/skills", async (Guid userId, UserSkillType? type, int? page, int? pageSize, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new ListUserSkillsQuery(userId, type, page, pageSize), cancellationToken)))
            .RequireAuthorization();

        var admin = routes.MapGroup("/admin/skill-proposals").RequireAuthorization(Authentication.AdminPolicy);

        admin.MapGet("", async (SkillStatus? status, int? page, int? pageSize, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new AdminListSkillProposalsQuery(status, page, pageSize), cancellationToken)));

        admin.MapPost("/{skillId}/approve", async (Guid skillId, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new ReviewSkillProposalCommand(skillId, Approve: true), cancellationToken)));

        admin.MapPost("/{skillId}/reject", async (Guid skillId, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new ReviewSkillProposalCommand(skillId, Approve: false), cancellationToken)));

        return routes;
    }
}
