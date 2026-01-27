using API.Services;

namespace API.Endpoints.Collections;

public static partial class CollectionsEndpoints
{
    public enum ImportSource
    {
        Manabox,
        Moxfield,
        Goldfish,
        Archidekt,
        Dragonshield,
        Delver
    }

    private static async Task<IResult> MapCollectionServiceException(
        CollectionServiceException exception,
        HttpContext context,
        IMessageLocalizer messageLocalizer,
        string? userId)
    {
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
