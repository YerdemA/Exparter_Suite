using System.ComponentModel.DataAnnotations;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Exparter.Api.Common;

/// <summary>How many requests a client may make (docs/api-conventions.md). Tests raise them.</summary>
public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimits";

    /// <summary>Requests per minute of a signed-in user, across the whole API.</summary>
    [Range(1, int.MaxValue)]
    public int UserPerMinute { get; init; } = 300;

    /// <summary>Requests per minute from one address without a signed-in user.</summary>
    [Range(1, int.MaxValue)]
    public int AnonymousPerMinute { get; init; } = 120;

    /// <summary>Attempts per minute from one address at what takes a password or a token.</summary>
    [Range(1, int.MaxValue)]
    public int CredentialsPerMinute { get; init; } = 30;

    /// <summary>Requests per quarter of an hour from one address at what sends an email.</summary>
    [Range(1, int.MaxValue)]
    public int EmailsPerQuarterHour { get; init; } = 10;
}

/// <summary>Limits that protect the API from floods, guessing and oversized input.</summary>
internal static class Hardening
{
    /// <summary>Sign-in, token refresh and one-time token confirmation: guessing is slowed down.</summary>
    public const string CredentialsPolicy = "credentials";

    /// <summary>Whatever sends an email: it costs, and it can be used to flood someone's inbox.</summary>
    public const string EmailPolicy = "email";

    /// <summary>
    /// No request body is large: files go straight to object storage through presigned
    /// URLs. Anything bigger is refused before it is read.
    /// </summary>
    public const long MaxRequestBodyBytes = 64 * 1024;

    public const string PayloadTooLargeCode = "payload_too_large";

    /// <summary>Clients send nothing over the hub but its handshake (docs/realtime.md).</summary>
    public const long MaxHubMessageBytes = 4 * 1024;

    public static IServiceCollection AddApiRateLimits(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RateLimitOptions>()
            .Bind(configuration.GetSection(RateLimitOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddRateLimiter(_ => { });
        services.AddOptions<RateLimiterOptions>()
            .Configure<IOptions<RateLimitOptions>>((limiter, limits) =>
            {
                var options = limits.Value;
                limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                    context.User.Identity?.IsAuthenticated == true
                        ? Window($"user:{context.User.GetUserId()}", options.UserPerMinute, TimeSpan.FromMinutes(1))
                        : Window($"ip:{AddressOf(context)}", options.AnonymousPerMinute, TimeSpan.FromMinutes(1)));
                limiter.AddPolicy(CredentialsPolicy, context =>
                    Window(AddressOf(context), options.CredentialsPerMinute, TimeSpan.FromMinutes(1)));
                limiter.AddPolicy(EmailPolicy, context =>
                    Window(AddressOf(context), options.EmailsPerQuarterHour, TimeSpan.FromMinutes(15)));
                limiter.OnRejected = async (rejected, cancellationToken) =>
                {
                    if (rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    {
                        rejected.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
                    }

                    await ApiProblem.WriteAsync(rejected.HttpContext, StatusCodes.Status429TooManyRequests, "rate_limited");
                };
            });
        return services;
    }

    /// <summary>Headers that keep browsers from doing anything with an API response but read it.</summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
            headers["Referrer-Policy"] = "no-referrer";

            // Responses carry personal data; nothing between the server and the client keeps a copy.
            headers.CacheControl = "no-store";
            return next(context);
        });

    /// <summary>
    /// Refuses a body announced as too large before reading it. Kestrel enforces the same
    /// limit on bodies that do not announce their length.
    /// </summary>
    public static IApplicationBuilder UseBodySizeLimit(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (context.Request.ContentLength > MaxRequestBodyBytes)
            {
                await ApiProblem.WriteAsync(context, StatusCodes.Status413PayloadTooLarge, PayloadTooLargeCode);
                return;
            }

            await next(context);
        });

    private static RateLimitPartition<string> Window(string key, int permits, TimeSpan window) =>
        RateLimitPartition.GetFixedWindowLimiter(
            key,
            _ => new FixedWindowRateLimiterOptions { PermitLimit = permits, Window = window, QueueLimit = 0 });

    // Behind a reverse proxy this is the proxy's address until forwarded headers are
    // trusted; the deployment slice sets that up.
    private static string AddressOf(HttpContext context) => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
