using API.Dtos.Decks;
using API.Extensions;
using Microsoft.AspNetCore.Mvc;

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
    
    private static void MapDeckQueries(RouteGroupBuilder group)
    {
        group.MapGet("/", GetAllDecksForUser)
            .RequireAuthorization()
            .WithSummary("Get decks for user")
            .WithDescription("Gets all decks from a given user")
            .Produces<IReadOnlyList<DeckDto>>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapGet("/{id:int}", GetDeckByIdAsync)
            .RequireAuthorization()
            .WithSummary("Get deck by ID")
            .WithDescription("Retrieves a specific deck by ID")
            .Produces<DeckDto>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapGet("/{deckId:int}/cards", GetDeckCardsAsync)
            .RequireAuthorization()
            .WithSummary("Get deck cards")
            .WithDescription("Retrieves all cards in a deck with optional filtering for maindeck, sideboard, and ownership status")
            .Produces<IEnumerable<DeckCardDto>>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapGet("/{deckId:int}/cards/{id:int}", GetDeckCardByIdAsync)
            .RequireAuthorization()
            .WithSummary("Get deck card by ID")
            .WithDescription("Retrieves specific deck card by ID")
            .Produces<DeckCardDto>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapGet("/{deckId:int}/export", ExportDeckAsync)
            .RequireAuthorization()
            .WithSummary("Export deck")
            .WithDescription("Returns the decklist as a list of strings with maindeck and sideboard sections")
            .Produces<string>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapGet("/{deckId:int}/missing-cards", GetMissingDeckCardsAsync)
            .RequireAuthorization()
            .WithSummary("Get missing deck cards")
            .WithDescription("Returns deck cards that are not owned in any user collection")
            .Produces<IEnumerable<DeckCardDto>>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapGet("/{deckId:int}/branches", GetDeckBranchesAsync)
            .RequireAuthorization()
            .WithSummary("Get deck branches")
            .WithDescription("Lists all branches for a deck")
            .Produces<IEnumerable<DeckBranchDto>>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapGet("/{deckId:int}/commits", GetDeckCommitsAsync)
            .RequireAuthorization()
            .WithSummary("Get deck commits")
            .WithDescription("Lists commit history for a deck")
            .Produces<IEnumerable<DeckCommitDto>>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapGet("/{deckId:int}/diff", GetDeckDiffAsync)
            .RequireAuthorization()
            .WithSummary("Diff two commits")
            .WithDescription("Compares two commits and returns added, removed, and modified cards")
            .Produces<DeckDiffDto>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");
    }
    
    private static void MapDeckCommands(RouteGroupBuilder group)
    {
        group.MapPost("/", CreateDeckAsync)
            .RequireAuthorization()
            .WithSummary("Create new deck")
            .WithDescription("Creates a new deck for the authenticated user")
            .Produces<DeckDto>()
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapPut("/{id:int}", UpdateDeckAsync)
            .RequireAuthorization()
            .WithSummary("Update deck")
            .WithDescription("Updates deck metadata (name, format)")
            .Produces<UpdateDeckResultDto>()
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapDelete("/{id:int}", DeleteDeckAsync)
            .RequireAuthorization()
            .WithSummary("Delete deck")
            .WithDescription("Deletes a deck and all its cards")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapPost("/{deckId:int}/cards", CreateDeckCardAsync)
            .RequireAuthorization()
            .WithSummary("Add card to deck")
            .WithDescription("Adds a new card to the deck")
            .Produces<DeckCardDto>()
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapPut("/{deckId:int}/cards/{id:int}", UpdateDeckCardAsync)
            .RequireAuthorization()
            .WithSummary("Update deck card")
            .WithDescription("Updates deck card quantities and owned card reference")
            .Produces<DeckCardDto>()
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapPut("/{deckId:int}/cards/{id:int}/versions", UpdateDeckCardVersionAsync)
            .RequireAuthorization()
            .WithSummary("Update deck card version")
            .WithDescription("Updates deck card printing using a Scryfall ID")
            .Produces<DeckCardDto>()
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapDelete("/{deckId:int}/cards/{id:int}", DeleteDeckCardAsync)
            .RequireAuthorization()
            .WithSummary("Remove card from deck")
            .WithDescription("Removes a card from the deck")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapPost("/import", ImportDeckFromDecklistAsync)
            .RequireAuthorization()
            .WithSummary("Import deck from text decklist")
            .WithDescription("Parses a decklist and creates a new deck with the imported cards")
            .Produces<ImportDeckDto>()
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapPost("/{deckId:int}/commits", CreateDeckCommitAsync)
            .RequireAuthorization()
            .WithSummary("Create deck commit")
            .WithDescription("Creates a commit from the current deck state")
            .Produces<DeckCommitDto>()
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapPost("/{deckId:int}/branches", CreateDeckBranchAsync)
            .RequireAuthorization()
            .WithSummary("Create deck branch")
            .WithDescription("Creates a new branch at a given commit")
            .Produces<DeckBranchDto>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapPost("/{deckId:int}/checkout", CheckoutDeckAsync)
            .RequireAuthorization()
            .WithSummary("Checkout deck commit")
            .WithDescription("Rebuilds the working copy from a commit")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");
    }
}
