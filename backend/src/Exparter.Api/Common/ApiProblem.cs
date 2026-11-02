using Microsoft.AspNetCore.Mvc;

namespace Exparter.Api.Common;

/// <summary>Writes RFC 9457 problem details carrying the Exparter <c>code</c> extension.</summary>
internal static class ApiProblem
{
    public const string ValidationFailedCode = "validation_failed";

    /// <summary>
    /// Gives a <c>code</c> to problem details produced by the framework itself, such as the
    /// 404 for an unknown route, so that every error response has one.
    /// </summary>
    public static void EnsureCode(ProblemDetailsContext context)
    {
        if (context.ProblemDetails.Extensions.ContainsKey("code"))
        {
            return;
        }

        context.ProblemDetails.Extensions["code"] = context.ProblemDetails.Status switch
        {
            StatusCodes.Status404NotFound => "not_found",
            StatusCodes.Status405MethodNotAllowed => "method_not_allowed",
            StatusCodes.Status415UnsupportedMediaType => "unsupported_media_type",
            _ => "error",
        };
    }

    public static async Task WriteAsync(
        HttpContext httpContext,
        int statusCode,
        string code,
        string? detail = null,
        IDictionary<string, string[]>? errors = null)
    {
        var problemDetails = new ProblemDetails { Status = statusCode, Detail = detail };
        problemDetails.Extensions["code"] = code;
        if (errors is not null)
        {
            problemDetails.Extensions["errors"] = errors;
        }

        // The default writer refuses a request that does not accept JSON, and throws here.
        // The error response is always JSON; without this, a client asking for something
        // else, such as an event stream on the hub, would get an empty 500 instead of the 401.
        httpContext.Request.Headers.Accept = "application/problem+json";
        httpContext.Response.StatusCode = statusCode;
        await httpContext.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(
            new ProblemDetailsContext { HttpContext = httpContext, ProblemDetails = problemDetails });
    }
}
