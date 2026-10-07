using System.Security.Claims;
using Exparter.Api.Common;
using Exparter.Application.Identity;
using Mediator;

namespace Exparter.Api.Identity;

internal sealed record RegisterRequest(string Email, string Username, string Password, bool AgeConfirmed);

internal sealed record LoginRequest(string Identifier, string Password);

internal sealed record RefreshTokenRequest(string RefreshToken);

internal sealed record OneTimeTokenRequest(string Token);

internal sealed record PasswordResetRequest(string Email);

internal sealed record PasswordResetConfirmRequest(string Token, string NewPassword);

internal sealed record AccountDeletionRequest(string Password);

internal static class IdentityEndpoints
{
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder routes)
    {
        var auth = routes.MapGroup("/auth");

        auth.MapPost("/register", async (RegisterRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(
                new RegisterCommand(request.Email, request.Username, request.Password, request.AgeConfirmed),
                cancellationToken);
            return Results.Created("/api/v1/me", result);
        });

        auth.MapPost("/login", async (LoginRequest request, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new LoginCommand(request.Identifier, request.Password), cancellationToken)));

        auth.MapPost("/refresh", async (RefreshTokenRequest request, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new RefreshTokenCommand(request.RefreshToken), cancellationToken)));

        auth.MapPost("/logout", async (RefreshTokenRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            await sender.Send(new LogoutCommand(request.RefreshToken), cancellationToken);
            return Results.NoContent();
        });

        auth.MapPost("/email-verification/request", async (ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
        {
            await sender.Send(new RequestEmailVerificationCommand(user.GetUserId()), cancellationToken);
            return Results.Accepted();
        }).RequireAuthorization();

        auth.MapPost("/email-verification/confirm", async (OneTimeTokenRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            await sender.Send(new ConfirmEmailVerificationCommand(request.Token), cancellationToken);
            return Results.NoContent();
        });

        auth.MapPost("/password-reset/request", async (PasswordResetRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            await sender.Send(new RequestPasswordResetCommand(request.Email), cancellationToken);
            return Results.Accepted();
        });

        auth.MapPost("/password-reset/confirm", async (PasswordResetConfirmRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            await sender.Send(new ConfirmPasswordResetCommand(request.Token, request.NewPassword), cancellationToken);
            return Results.NoContent();
        });

        var me = routes.MapGroup("/me").RequireAuthorization();

        me.MapGet("", async (ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetAccountQuery(user.GetUserId()), cancellationToken)));

        me.MapPost("/deletion-request", async (AccountDeletionRequest request, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(
                new RequestAccountDeletionCommand(user.GetUserId(), request.Password),
                cancellationToken);
            return Results.Accepted(value: result);
        });

        return routes;
    }
}
