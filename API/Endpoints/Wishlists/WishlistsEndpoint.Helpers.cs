using API.Services;

namespace API.Endpoints.Wishlists;

public static partial class WishlistsEndpoint
{
    private static async Task<IResult> MapWishlistServiceException(
        WishlistServiceException exception,
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
            StatusCodes.Status404NotFound => Results.NotFound(await messageLocalizer.GetMessageAsync(
                userId ?? string.Empty,
                exception.Message,
                context.RequestAborted)),
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
