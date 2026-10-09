using System.Security.Claims;
using Exparter.Api.Common;
using Exparter.Application.Profiles;
using Exparter.Domain.Profiles;
using Mediator;

namespace Exparter.Api.Profiles;

internal sealed record UpdateProfileRequest(
    string DisplayName,
    string? Bio,
    string? City,
    bool AcceptsOnline,
    bool AcceptsInPerson);

internal sealed record AvatarUploadUrlRequest(string ContentType, long ContentLength);

internal sealed record SetAvatarRequest(string ObjectKey);

internal static class ProfileEndpoints
{
    public static IEndpointRouteBuilder MapProfileEndpoints(this IEndpointRouteBuilder routes)
    {
        var me = routes.MapGroup("/me").RequireAuthorization();

        me.MapGet("/profile", async (ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetOwnProfileQuery(user.GetUserId()), cancellationToken)));

        me.MapPut("/profile", async (UpdateProfileRequest request, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(
                new UpdateOwnProfileCommand(
                    user.GetUserId(),
                    request.DisplayName,
                    request.Bio,
                    request.City,
                    request.AcceptsOnline,
                    request.AcceptsInPerson),
                cancellationToken)));

        me.MapPost("/avatar/upload-url", async (AvatarUploadUrlRequest request, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(
                new CreateAvatarUploadUrlCommand(user.GetUserId(), request.ContentType, request.ContentLength),
                cancellationToken)));

        me.MapPut("/avatar", async (SetAvatarRequest request, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new SetAvatarCommand(user.GetUserId(), request.ObjectKey), cancellationToken)));

        me.MapDelete("/avatar", async (ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
        {
            await sender.Send(new DeleteAvatarCommand(user.GetUserId()), cancellationToken);
            return Results.NoContent();
        });

        routes.MapGet("/users/{userId}", async (Guid userId, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetUserProfileQuery(userId), cancellationToken)))
            .RequireAuthorization();

        // A fixed reference list: nothing to look up, so it does not go through the mediator.
        routes.MapGet("/cities", () => Results.Ok(Cities.All.Select(city => new CityDto(city.Code, city.Name))));

        return routes;
    }
}
