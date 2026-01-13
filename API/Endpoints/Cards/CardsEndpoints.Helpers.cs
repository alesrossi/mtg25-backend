using API.Helpers;
using API.Services;

namespace API.Endpoints.Cards;

public static partial class CardsEndpoints
{
    private static IResult MapCardsServiceException(CardsServiceException exception, HttpContext context)
    {
        if (!string.IsNullOrWhiteSpace(exception.Title))
        {
            return ProblemResultFactory.Create(
                context,
                exception.StatusCode,
                exception.Title!,
                exception.Detail,
                exception.ErrorCode);
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
