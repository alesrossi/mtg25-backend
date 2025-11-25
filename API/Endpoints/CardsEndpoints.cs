using API.Dtos.Cards;
using Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace API.Endpoints;

public static partial class CardsEndpoints
{
    public static void MapCardsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/cards").WithTags("CollectionCards");

        MapCardQueries(group);
        MapCardCommands(group);
    }
}
