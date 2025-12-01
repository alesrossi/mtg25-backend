using API.Extensions;

namespace API.Endpoints.Cards;

public static partial class CardsEndpoints
{
    public static void MapCardsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/cards")
            .WithTags("CollectionCards")
            .WithProblemDetailsContract();

        MapCardQueries(group);
        MapCardCommands(group);
    }
}
