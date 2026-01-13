using API.Services;
using Microsoft.AspNetCore.Http;

namespace API.Endpoints.Decks;

public static partial class DecksEndpoint
{
    private static async Task<IResult> MapDeckServiceException(
        DeckServiceException exception,
        HttpContext context,
        IMessageLocalizer messageLocalizer,
        string? userId)
    {
        if (exception.StatusCode == StatusCodes.Status500InternalServerError && exception.ProblemTitle is not null)
        {
            var problemDetail = exception.ProblemDetail;
            if (!string.IsNullOrWhiteSpace(problemDetail))
            {
                problemDetail = await messageLocalizer.GetMessageAsync(
                    userId ?? string.Empty,
                    problemDetail,
                    context.RequestAborted);
            }

            return Results.Problem(
                detail: problemDetail,
                statusCode: exception.StatusCode,
                title: exception.ProblemTitle);
        }

        return exception.StatusCode switch
        {
            StatusCodes.Status400BadRequest => exception.IncludeBody
                ? Results.BadRequest(await LocalizeBodyAsync(context, messageLocalizer, userId, exception.Body))
                : Results.BadRequest(),
            StatusCodes.Status401Unauthorized => Results.Unauthorized(),
            StatusCodes.Status404NotFound => exception.IncludeBody
                ? Results.NotFound(await LocalizeBodyAsync(context, messageLocalizer, userId, exception.Body))
                : Results.NotFound(),
            _ => Results.StatusCode(exception.StatusCode)
        };
    }

    private static async Task<object?> LocalizeBodyAsync(
        HttpContext context,
        IMessageLocalizer messageLocalizer,
        string? userId,
        object? body)
    {
        if (body is string messageKey)
        {
            return await messageLocalizer.GetMessageAsync(userId ?? string.Empty, messageKey, context.RequestAborted);
        }

        return body;
    }
}
