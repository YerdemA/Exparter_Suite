using System.Text.Json;
using Exparter.Application.Common;
using Exparter.Domain.Common;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;

namespace Exparter.Api.Common;

internal sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        switch (exception)
        {
            case ValidationException validation:
                var errors = validation.Errors
                    .GroupBy(error => JsonNamingPolicy.CamelCase.ConvertName(error.PropertyName))
                    .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray());
                await ApiProblem.WriteAsync(
                    httpContext,
                    StatusCodes.Status400BadRequest,
                    ApiProblem.ValidationFailedCode,
                    "One or more fields are invalid.",
                    errors);
                return true;

            case BadHttpRequestException badRequest:
                await ApiProblem.WriteAsync(
                    httpContext,
                    badRequest.StatusCode,
                    ApiProblem.ValidationFailedCode,
                    "The request body could not be read.");
                return true;

            case AppException app:
                await ApiProblem.WriteAsync(httpContext, StatusOf(app.Kind), app.Code, app.Message);
                return true;

            case DomainException domain:
                await ApiProblem.WriteAsync(httpContext, StatusCodes.Status409Conflict, domain.Code, domain.Message);
                return true;

            default:
                logger.LogError(exception, "Unhandled exception");
                await ApiProblem.WriteAsync(httpContext, StatusCodes.Status500InternalServerError, "internal_error");
                return true;
        }
    }

    private static int StatusOf(AppErrorKind kind) => kind switch
    {
        AppErrorKind.BadRequest => StatusCodes.Status400BadRequest,
        AppErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
        AppErrorKind.Forbidden => StatusCodes.Status403Forbidden,
        AppErrorKind.NotFound => StatusCodes.Status404NotFound,
        AppErrorKind.Conflict => StatusCodes.Status409Conflict,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
