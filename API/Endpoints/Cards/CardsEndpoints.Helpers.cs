using API.Helpers;
using API.Services;

namespace API.Endpoints.Cards;

public static partial class CardsEndpoints
{
    private static async Task<IResult> MapCardsServiceException(
        CardsServiceException exception,
        HttpContext context,
        IMessageLocalizer messageLocalizer,
        string? userId)
    {
        if (!string.IsNullOrWhiteSpace(exception.Title))
        {
            var detail = string.IsNullOrWhiteSpace(exception.Detail)
                ? exception.Detail
                : await messageLocalizer.GetMessageAsync(userId ?? string.Empty, exception.Detail, context.RequestAborted);
            return ProblemResultFactory.Create(
                context,
                exception.StatusCode,
                exception.Title!,
                detail,
                exception.ErrorCode);
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
