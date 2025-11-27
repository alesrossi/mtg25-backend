using API.Dtos.Cards;
using API.Extensions;
using Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace API.Endpoints;

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
