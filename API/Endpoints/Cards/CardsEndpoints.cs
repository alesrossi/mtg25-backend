using API.Dtos.Cards;
using API.Extensions;
using Core.Models;
using Microsoft.AspNetCore.Mvc;

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
    
    private static void MapCardQueries(RouteGroupBuilder group)
    {
        group.MapGet("/{id:int}", GetCardFromId)
            .RequireAuthorization()
            .WithSummary("Get card by ID")
            .WithDescription("Retrieves card by internal database ID")
            .Produces<Card?>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapGet("/search/{find}", SearchCards)
            .RequireAuthorization()
            .WithSummary("Search cards by name")
            .WithDescription("Searches cards by name with partial matching")
            .Produces<List<MinimalCardDto>>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapGet("/{name}/versions", GetCardVersionsAsync)
            .RequireAuthorization()
            .WithSummary("Retrieves all versions of a card")
            .WithDescription("Returns all card dtos for a given exact card name")
            .Produces<List<KeyValuePair<string, ScryfallCardDto>>>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapGet("/sf/name/{name}", GetCardFromExactName)
            .RequireAuthorization()
            .WithSummary("Returns Scryfall card from name")
            .WithDescription("Returns Scryfall card with all fields, from exact name")
            .Produces<ScryfallCardDto>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapGet("/sf/id/{id}", GetCardFromScryfallId)
            .RequireAuthorization()
            .WithSummary("Returns Scryfall card from id")
            .WithDescription("Returns Scryfall card with all fields, from id")
            .Produces<ScryfallCardDto>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");
    }
    
    private static void MapCardCommands(RouteGroupBuilder group)
    {
        group.MapPut("/{id:int}", UpdateCardFromIdAsync)
            .RequireAuthorization()
            .WithSummary("Update Card")
            .WithDescription("Updates card from form")
            .Produces<Card>()
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");
        
        group.MapPut("/{id:int}/versions", UpdateCardVersionFromIdAsync)
            .RequireAuthorization()
            .WithSummary("Update Card")
            .WithDescription("Updates card from form")
            .Produces<Card>()
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");
        
        group.MapDelete("/{id:int}", DeleteCardFromIdAsync)
            .RequireAuthorization()
            .WithSummary("Delete card")
            .WithDescription("Removes card by ID from collection")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapPost("/", AddNewCardAsync)
            .RequireAuthorization()
            .WithSummary("Add new card")
            .WithDescription("Adds card to collection with properties")
            .Produces<Card>()
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapPost("/card-list", AddCardListAsync)
            .RequireAuthorization()
            .WithSummary("Process card list")
            .WithDescription("Processes card names and returns Scryfall card data")
            .Produces<LinkedList<ScryfallCardDto>>()
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");
    }
}
