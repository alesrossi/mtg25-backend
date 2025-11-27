using System.Collections.Generic;
using API.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;

namespace API.Filters;

public class ProblemDetailsEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var result = await next(context);

        if (result is IResult httpResult)
        {
            if (IsProblemResult(httpResult)) return result;

            if (httpResult is IStatusCodeHttpResult statusResult &&
                statusResult.StatusCode is int statusCode &&
                statusCode >= 400)
            {
                var (detail, extensions) = ExtractDetailAndExtensions(httpResult);
                var (title, errorCode) = ResolveDefaults(statusCode);

                return ProblemResultFactory.Create(
                    context.HttpContext,
                    statusCode,
                    title,
                    detail,
                    errorCode,
                    extensions);
            }
        }

        return result;
    }

    private static bool IsProblemResult(IResult result)
    {
        if (result is IValueHttpResult valueResult)
        {
            return valueResult.Value is ProblemDetails;
        }

        return false;
    }

    private static (string? detail, IDictionary<string, object?>? extensions) ExtractDetailAndExtensions(IResult result)
    {
        if (result is not IValueHttpResult valueResult)
        {
            return (null, null);
        }

        var value = valueResult.Value;
        if (value is ValidationProblemDetails validation)
        {
            var extensions = new Dictionary<string, object?>(validation.Extensions)
            {
                ["errors"] = validation.Errors
            };
            return (validation.Detail, extensions);
        }

        if (value is ProblemDetails problem)
        {
            var extensions = problem.Extensions.Count > 0
                ? new Dictionary<string, object?>(problem.Extensions)
                : null;
            return (problem.Detail, extensions);
        }

        if (value is string message)
        {
            return (message, null);
        }

        var errorsProperty = value?.GetType().GetProperty("errors");
        if (errorsProperty != null)
        {
            var errorsValue = errorsProperty.GetValue(value);
            return ("See errors for additional information.", new Dictionary<string, object?>
            {
                ["errors"] = errorsValue
            });
        }

        return (value?.ToString(), null);
    }

    private static (string Title, string ErrorCode) ResolveDefaults(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => ("Invalid request", "bad-request"),
        StatusCodes.Status401Unauthorized => ("Authentication required", "auth-required"),
        StatusCodes.Status403Forbidden => ("Forbidden", "auth-forbidden"),
        StatusCodes.Status404NotFound => ("Resource not found", "resource-not-found"),
        StatusCodes.Status409Conflict => ("Conflict", "conflict"),
        StatusCodes.Status422UnprocessableEntity => ("Unprocessable entity", "unprocessable-entity"),
        StatusCodes.Status500InternalServerError => ("Server error", "server-error"),
        _ when statusCode >= 500 => ("Server error", "server-error"),
        _ => ("Request failed", "request-failed")
    };
}
