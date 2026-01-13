using API.Services;
using Microsoft.AspNetCore.Http;

namespace API.Endpoints.Decks;

public static partial class DecksEndpoint
{
    private static IResult MapDeckServiceException(DeckServiceException exception)
    {
        if (exception.StatusCode == StatusCodes.Status500InternalServerError && exception.ProblemTitle is not null)
        {
            return Results.Problem(
                detail: exception.ProblemDetail,
                statusCode: exception.StatusCode,
                title: exception.ProblemTitle);
        }

        return exception.StatusCode switch
        {
            StatusCodes.Status400BadRequest => exception.IncludeBody ? Results.BadRequest(exception.Body) : Results.BadRequest(),
            StatusCodes.Status401Unauthorized => Results.Unauthorized(),
            StatusCodes.Status404NotFound => exception.IncludeBody ? Results.NotFound(exception.Body) : Results.NotFound(),
            _ => Results.StatusCode(exception.StatusCode)
        };
    }
}
