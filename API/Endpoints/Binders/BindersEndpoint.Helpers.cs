using API.Services;

namespace API.Endpoints.Binders;

public static partial class BindersEndpoint
{
    private static IResult MapBindersServiceException(BindersServiceException exception)
    {
        return exception.StatusCode switch
        {
            StatusCodes.Status400BadRequest => exception.IncludeBody ? Results.BadRequest(exception.Body) : Results.BadRequest(),
            StatusCodes.Status401Unauthorized => Results.Unauthorized(),
            StatusCodes.Status404NotFound => exception.IncludeBody ? Results.NotFound(exception.Body) : Results.NotFound(),
            _ => Results.StatusCode(exception.StatusCode)
        };
    }
}
