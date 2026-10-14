namespace Exparter.Api.Common;

/// <summary>What error reports carry beyond the error itself (ADR 0013).</summary>
internal static class ErrorReporting
{
    /// <summary>
    /// Tags error reports with the signed-in user's id, and nothing else about them, so
    /// that it shows how many users an error reaches (ADR 0013).
    /// </summary>
    public static IApplicationBuilder UseErrorReportUser(this IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var userId = context.User.GetUserId().ToString();
                SentrySdk.ConfigureScope(scope => scope.User = new SentryUser { Id = userId });
            }

            return next(context);
        });
}
