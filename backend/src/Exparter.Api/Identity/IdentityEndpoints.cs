using System.Security.Claims;
using Exparter.Api.Common;
using Exparter.Application.Common;
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

internal sealed record ChangeUsernameRequest(string Username);

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
        }).RequireRateLimiting(Hardening.EmailPolicy);

        auth.MapPost("/login", async (LoginRequest request, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new LoginCommand(request.Identifier, request.Password), cancellationToken)))
            .RequireRateLimiting(Hardening.CredentialsPolicy);

        auth.MapPost("/refresh", async (RefreshTokenRequest request, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new RefreshTokenCommand(request.RefreshToken), cancellationToken)))
            .RequireRateLimiting(Hardening.CredentialsPolicy);

        auth.MapPost("/logout", async (RefreshTokenRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            await sender.Send(new LogoutCommand(request.RefreshToken), cancellationToken);
            return Results.NoContent();
        }).RequireRateLimiting(Hardening.CredentialsPolicy);

        auth.MapPost("/email-verification/request", async (ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
        {
            await sender.Send(new RequestEmailVerificationCommand(user.GetUserId()), cancellationToken);
            return Results.Accepted();
        }).RequireAuthorization().RequireRateLimiting(Hardening.EmailPolicy);

        auth.MapPost("/email-verification/confirm", async (OneTimeTokenRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            await sender.Send(new ConfirmEmailVerificationCommand(request.Token), cancellationToken);
            return Results.NoContent();
        }).RequireRateLimiting(Hardening.CredentialsPolicy);

        auth.MapPost("/password-reset/request", async (PasswordResetRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            await sender.Send(new RequestPasswordResetCommand(request.Email), cancellationToken);
            return Results.Accepted();
        }).RequireRateLimiting(Hardening.EmailPolicy);

        auth.MapPost("/password-reset/confirm", async (PasswordResetConfirmRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            await sender.Send(new ConfirmPasswordResetCommand(request.Token, request.NewPassword), cancellationToken);
            return Results.NoContent();
        }).RequireRateLimiting(Hardening.CredentialsPolicy);

        var me = routes.MapGroup("/me").RequireAuthorization();

        me.MapGet("", async (ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetAccountQuery(user.GetUserId()), cancellationToken)));

        me.MapGet("/interaction-requirements", async (ClaimsPrincipal user, IInteractionRequirements requirements, CancellationToken cancellationToken) =>
            Results.Ok(await requirements.GetAsync(user.GetUserId(), cancellationToken)));

        me.MapPut("/username", async (ChangeUsernameRequest request, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new ChangeUsernameCommand(user.GetUserId(), request.Username), cancellationToken)));

        me.MapPost("/deletion-request", async (AccountDeletionRequest request, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(
                new RequestAccountDeletionCommand(user.GetUserId(), request.Password),
                cancellationToken);
            return Results.Accepted(value: result);
        }).RequireRateLimiting(Hardening.CredentialsPolicy);

        return routes;
    }
}
