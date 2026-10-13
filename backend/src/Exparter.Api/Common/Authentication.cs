using System.Security.Claims;
using Exparter.Domain.Identity;
using Exparter.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Exparter.Api.Common;

internal static class Authentication
{
    public const string AdminPolicy = "Admin";

    public static IServiceCollection AddApiAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>, TimeProvider>((bearer, jwt, timeProvider) =>
            {
                // Keep the claim names exactly as issued instead of the legacy XML-style names.
                bearer.MapInboundClaims = false;
                bearer.TimeProvider = timeProvider;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Value.Issuer,
                    ValidAudience = jwt.Value.Audience,
                    IssuerSigningKey = jwt.Value.CreateSecurityKey(),
                    NameClaimType = JwtClaims.Subject,
                    RoleClaimType = JwtClaims.Role,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
                bearer.Events = new JwtBearerEvents
                {
                    // Browsers cannot set headers on a WebSocket, so the real-time hub takes the
                    // token from the query string instead (the standard SignalR arrangement).
                    OnMessageReceived = context =>
                    {
                        if (context.HttpContext.Request.Path.StartsWithSegments(Exparter.Api.Messaging.RealtimeHub.Path)
                            && context.Request.Query.TryGetValue("access_token", out var token))
                        {
                            context.Token = token;
                        }

                        return Task.CompletedTask;
                    },
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        var code = context.AuthenticateFailure is SecurityTokenExpiredException
                            ? "token_expired"
                            : "unauthorized";
                        await ApiProblem.WriteAsync(context.HttpContext, StatusCodes.Status401Unauthorized, code);
                    },
                    OnForbidden = context =>
                        ApiProblem.WriteAsync(context.HttpContext, StatusCodes.Status403Forbidden, "admin_required"),
                };
            });
        services.AddAuthorizationBuilder()
            .AddPolicy(AdminPolicy, policy => policy.RequireRole(nameof(UserRole.Admin)));
        return services;
    }

    public static Guid GetUserId(this ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(JwtClaims.Subject)
            ?? throw new InvalidOperationException("The authenticated principal has no subject claim."));
}
