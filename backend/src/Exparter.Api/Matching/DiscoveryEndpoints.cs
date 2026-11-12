using System.Security.Claims;
using Exparter.Api.Common;
using Exparter.Application.Matching;
using Mediator;

namespace Exparter.Api.Matching;

internal static class DiscoveryEndpoints
{
    public static IEndpointRouteBuilder MapDiscoveryEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet(
            "/discovery/users",
            async (
                Guid? skillId,
                Guid? categoryId,
                string? city,
                string? q,
                string? cursor,
                int? limit,
                ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(
                    new DiscoverUsersQuery(user.GetUserId(), skillId, categoryId, city, q, cursor, limit),
                    cancellationToken)))
            .RequireAuthorization();

        return routes;
    }
}
