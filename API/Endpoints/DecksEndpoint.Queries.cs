using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using API.Dtos.Decks;
using API.Services;
using Core.Interfaces;
using Core.Models;
using Core.Models.Identity;
using Core.Specifications;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace API.Endpoints;

public static partial class DecksEndpoint
{
    private static void MapDeckQueries(RouteGroupBuilder group)
    {
        group.MapGet("/", GetAllDecksForUser)
            .RequireAuthorization()
            .WithSummary("Get decks for user")
            .WithDescription("Gets all decks from a given user")
            .Produces<IReadOnlyList<DeckDto>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{id:int}", GetDeckByIdAsync)
            .RequireAuthorization()
            .WithSummary("Get deck by ID")
            .WithDescription("Retrieves a specific deck by ID")
            .Produces<DeckDto>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{deckId:int}/cards", GetDeckCardsAsync)
            .RequireAuthorization()
            .WithSummary("Get deck cards")
            .WithDescription("Retrieves all cards in a deck with optional filtering for maindeck, sideboard, and ownership status")
            .Produces<IEnumerable<DeckCardDto>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{deckId:int}/cards/{id:int}", GetDeckCardByIdAsync)
            .RequireAuthorization()
            .WithSummary("Get deck card by ID")
            .WithDescription("Retrieves specific deck card by ID")
            .Produces<DeckCardDto>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{deckId:int}/export", ExportDeckAsync)
            .RequireAuthorization()
            .WithSummary("Export deck")
            .WithDescription("Returns the decklist as a list of strings with maindeck and sideboard sections")
            .Produces<IReadOnlyList<string>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{deckId:int}/missing-cards", GetMissingDeckCardsAsync)
            .RequireAuthorization()
            .WithSummary("Get missing deck cards")
            .WithDescription("Returns deck cards that are not owned in any user collection")
            .Produces<IEnumerable<DeckCardDto>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> GetAllDecksForUser(
        IUnitOfWork unitOfWork,
        [FromServices] UserManager<AppUser> userManager,
        HttpContext context)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null) return Results.Unauthorized();

        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return Results.Unauthorized();

        var decks = await unitOfWork.Repository<Deck>().ListAsync(new DecksWIthOwnerSpecification(user.Id), tracking: false);
        if (decks is null || decks.Count <= 0) return Results.NotFound("No decks found");

        var deckDtos = decks.Select(MapToDto).ToList();
        return Results.Ok(deckDtos);
    }

    private static async Task<IResult> GetDeckByIdAsync(
        int id,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Results.Unauthorized();

        var deck = await unitOfWork.Repository<Deck>().GetByIdAsync(id, tracking: false);
        if (deck == null || deck.OwnerId != userId) return Results.NotFound();

        return Results.Ok(MapToDto(deck));
    }

    private static async Task<IResult> GetDeckCardsAsync(
        int deckId,
        bool maindeckOnly = false,
        bool sideboardOnly = false,
        bool? ownedOnly = null,
        DeckCardService deckCardService = null!,
        IUnitOfWork unitOfWork = null!,
        ClaimsPrincipal user = null!)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Results.Unauthorized();

        var deck = await unitOfWork.Repository<Deck>().GetByIdAsync(deckId, tracking: false);
        if (deck == null || deck.OwnerId != userId) return Results.NotFound();

        var deckCards = await deckCardService.GetDeckCardsAsync(deckId, maindeckOnly, sideboardOnly, ownedOnly);
        return Results.Ok(deckCards);
    }

    private static async Task<IResult> GetDeckCardByIdAsync(
        int deckId,
        int id,
        DeckCardService deckCardService,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Results.Unauthorized();

        var deck = await unitOfWork.Repository<Deck>().GetByIdAsync(deckId, tracking: false);
        if (deck == null || deck.OwnerId != userId) return Results.NotFound();

        var deckCard = await deckCardService.GetDeckCardByIdAsync(id);
        if (deckCard == null || deckCard.DeckId != deckId) return Results.NotFound();

        return Results.Ok(deckCard);
    }

    private static async Task<IResult> GetMissingDeckCardsAsync(
        int deckId,
        DeckCardService deckCardService,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Results.Unauthorized();

        var deck = await unitOfWork.Repository<Deck>().GetByIdAsync(deckId, tracking: false);
        if (deck == null || deck.OwnerId != userId) return Results.NotFound();

        var missingCards = (await deckCardService.GetDeckCardsAsync(deckId, ownedOnly: false)).ToList();
        return Results.Ok(missingCards);
    }

    private static async Task<IResult> ExportDeckAsync(
        int deckId,
        DeckCardService deckCardService,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Results.Unauthorized();

        var deck = await unitOfWork.Repository<Deck>().GetByIdAsync(deckId, tracking: false);
        if (deck == null || deck.OwnerId != userId) return Results.NotFound();

        var deckCards = (await deckCardService.GetDeckCardsAsync(deckId)).ToList();

        if (deckCards.Count == 0)
        {
            return Results.Ok(Array.Empty<string>());
        }

        var maindeckLines = deckCards
            .Where(card => card.MaindeckQuantity > 0)
            .OrderBy(card => card.Name, StringComparer.OrdinalIgnoreCase)
            .Select(card => $"{card.MaindeckQuantity} {card.Name}")
            .ToList();

        var sideboardLines = deckCards
            .Where(card => card.SideboardQuantity > 0)
            .OrderBy(card => card.Name, StringComparer.OrdinalIgnoreCase)
            .Select(card => $"{card.SideboardQuantity} {card.Name}")
            .ToList();

        var exportedLines = new List<string>(maindeckLines);

        if (sideboardLines.Count > 0)
        {
            if (exportedLines.Count > 0)
            {
                exportedLines.Add(string.Empty);
            }

            exportedLines.AddRange(sideboardLines);
        }

        return Results.Ok(exportedLines);
    }
}
