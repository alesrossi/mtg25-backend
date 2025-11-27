using API.Dtos.Decks;
using API.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace API.Endpoints;

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
