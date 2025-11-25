using API.Dtos.Decks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace API.Endpoints;

public static partial class DecksEndpoint
{
    public static void MapDecksEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/decks").WithTags("Decks");

        MapDeckQueries(group);
        MapDeckCommands(group);
    }
}
