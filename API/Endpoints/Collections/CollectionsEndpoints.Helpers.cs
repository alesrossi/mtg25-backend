using API.Services;
using Microsoft.AspNetCore.Http;

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

    private static IResult MapCollectionServiceException(CollectionServiceException exception)
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
