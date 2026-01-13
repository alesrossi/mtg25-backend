using API.Services;

namespace API.Endpoints.Wishlists;

public static partial class WishlistsEndpoint
{
    private static IResult MapWishlistServiceException(WishlistServiceException exception)
    {
        return exception.StatusCode switch
        {
            StatusCodes.Status400BadRequest => exception.IncludeBody ? Results.BadRequest(exception.Body) : Results.BadRequest(),
            StatusCodes.Status401Unauthorized => Results.Unauthorized(),
            StatusCodes.Status404NotFound => Results.NotFound(),
            _ => Results.StatusCode(exception.StatusCode)
        };
    }
}
