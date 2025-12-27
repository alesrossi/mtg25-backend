using API.Dtos.Cards;
using API.Extensions;
using API.Helpers;
using Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints.Collections;

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
    
    private static void MapCollectionQueries(RouteGroupBuilder group)
    {
        group.MapGet("/{id:int}", GetCollectionFromIdAsync)
            .RequireAuthorization()
            .WithSummary("Get collection by ID")
            .WithDescription("Retrieves specific collection by ID")
            .Produces<Collection>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapGet("/{id:int}/cards", GetCardsFromCollectionAsync)
            .RequireAuthorization()
            .WithSummary("Get cards from collection")
            .WithDescription("Retrieves paginated list of cards from a specific collection with filtering, sorting, searching, and optional grouping")
            .Produces<Pagination<Card>>()
            .Produces<GroupedCardsPaginationDto>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapGet("/", GetAllCollectionsForUser)
            .RequireAuthorization()
            .WithSummary("Get user's collections")
            .WithDescription("Returns all collections owned by authenticated user")
            .Produces<List<Collection>>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");
    }
    
    private static void MapCollectionCommands(RouteGroupBuilder group)
    {
        group.MapPost("/", AddNewCollectionAsync)
            .RequireAuthorization()
            .WithSummary("Create new collection")
            .WithDescription("Creates new collection with name and color")
            .Produces<Collection>()
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapPut("/{id:int}", UpdateCollectionAsync)
            .RequireAuthorization()
            .WithSummary("Update existing collection")
            .WithDescription("Updates Existing collection with name and color")
            .Produces<Collection>()
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapDelete("/{id:int}", DeleteCollectionAsync)
            .RequireAuthorization()
            .WithSummary("Delete collection")
            .WithDescription("Deletes collection from given id")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapPatch("/{id:int}/import", ImportCardList)
            .RequireAuthorization()
            .WithSummary("Import cards from CSV")
            .WithDescription("Imports cards from CSV file into collection")
            .Produces<List<Card>>()
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json")
            .DisableAntiforgery();

        group.MapDelete("/{id:int}/mass-delete", MassDeleteCardsFromCollection)
            .RequireAuthorization()
            .WithSummary("Mass delete cards")
            .WithDescription("Deletes multiple cards from collection by ID list")
            .Produces<int>()
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");
    }
}
