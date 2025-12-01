using API.Extensions;

namespace API.Endpoints.Decks;

public static partial class DecksEndpoint
{
    public static void MapDecksEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/decks")
            .WithTags("Decks")
            .WithProblemDetailsContract();

        MapDeckQueries(group);
        MapDeckCommands(group);
    }
}
