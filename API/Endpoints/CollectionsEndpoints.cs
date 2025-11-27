using API.Dtos.Cards;
using API.Dtos.Collections;
using API.Extensions;
using API.Helpers;
using Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace API.Endpoints;

public static partial class CollectionsEndpoints
{
    public static void MapCollectionsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/collections")
            .WithTags("Collections")
            .WithProblemDetailsContract();

        MapCollectionQueries(group);
        MapCollectionCommands(group);
    }
}
