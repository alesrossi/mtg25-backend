using System.Collections.Generic;
using System.Security.Claims;
using API.Dtos.Decks;
using API.Services;
using Core.Interfaces;
using Core.Models;
using Core.Models.Identity;
using Core.Specifications;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints;

public static class DecksEndpoint
{
    public static void MapDecksEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/decks").WithTags("Decks");
        
        group.MapGet("/", GetAllDecksForUser)
            .RequireAuthorization()
            .WithSummary("Get decks for user")
            .WithDescription("Gets all decks from a given user")
            .Produces<IReadOnlyList<DeckDto>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
        
        group.MapPost("/", CreateDeckAsync)
            .RequireAuthorization()
            .WithSummary("Create new deck")
            .WithDescription("Creates a new deck for the authenticated user")
            .Produces<DeckDto>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);
        
        group.MapGet("/{id:int}", GetDeckByIdAsync)
            .RequireAuthorization()
            .WithSummary("Get deck by ID")
            .WithDescription("Retrieves a specific deck by ID")
            .Produces<DeckDto>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
        
        group.MapPut("/{id:int}", UpdateDeckAsync)
            .RequireAuthorization()
            .WithSummary("Update deck")
            .WithDescription("Updates deck metadata (name, format)")
            .Produces<DeckDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
        
        group.MapDelete("/{id:int}", DeleteDeckAsync)
            .RequireAuthorization()
            .WithSummary("Delete deck")
            .WithDescription("Deletes a deck and all its cards")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
        
        group.MapGet("/{deckId:int}/cards", GetDeckCardsAsync)
            .RequireAuthorization()
            .WithSummary("Get deck cards")
            .WithDescription("Retrieves all cards in a deck with optional filtering for maindeck, sideboard, and ownership status")
            .Produces<IEnumerable<DeckCardDto>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
        
        group.MapGet("/{deckId:int}/cards/{id:int}", GetDeckCardByIdAsync)
            .RequireAuthorization()
            .WithSummary("Get deck card by ID")
            .WithDescription("Retrieves specific deck card by ID")
            .Produces<DeckCardDto>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
        
        group.MapPost("/{deckId:int}/cards", CreateDeckCardAsync)
            .RequireAuthorization()
            .WithSummary("Add card to deck")
            .WithDescription("Adds a new card to the deck")
            .Produces<DeckCardDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
        
        group.MapPut("/{deckId:int}/cards/{id:int}", UpdateDeckCardAsync)
            .RequireAuthorization()
            .WithSummary("Update deck card")
            .WithDescription("Updates deck card quantities and owned card reference")
            .Produces<DeckCardDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
        
        group.MapDelete("/{deckId:int}/cards/{id:int}", DeleteDeckCardAsync)
            .RequireAuthorization()
            .WithSummary("Remove card from deck")
            .WithDescription("Removes a card from the deck")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
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
        if (user is null)  return Results.Unauthorized();
        
        var decks = await unitOfWork.Repository<Deck>().ListAsync(new DecksWIthOwnerSpecification(user.Id));
        if (decks is null || decks.Count <= 0) return Results.NotFound("No decks found");
        
        var deckDtos = decks.Select(MapToDto).ToList();
        return Results.Ok(deckDtos);
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

        // Verify deck ownership
        var deck = await unitOfWork.Repository<Deck>().GetByIdAsync(deckId);
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

        // Verify deck ownership
        var deck = await unitOfWork.Repository<Deck>().GetByIdAsync(deckId);
        if (deck == null || deck.OwnerId != userId) return Results.NotFound();

        var deckCard = await deckCardService.GetDeckCardByIdAsync(id);
        if (deckCard == null || deckCard.DeckId != deckId) return Results.NotFound();

        return Results.Ok(deckCard);
    }

    private static async Task<IResult> CreateDeckCardAsync(
        int deckId,
        CreateDeckCardDto createDto,
        DeckCardService deckCardService,
        [FromServices] IValidationService validationService,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Results.Unauthorized();

        // Verify deck ownership
        var deck = await unitOfWork.Repository<Deck>().GetByIdAsync(deckId);
        if (deck == null || deck.OwnerId != userId) return Results.NotFound();

        var (isValid, errors) = validationService.ValidateModel(createDto);
        if (!isValid)
        {
            return Results.BadRequest(new { errors });
        }

        if (createDto.MaindeckQuantity + createDto.SideboardQuantity <= 0)
        {
            var quantityErrors = new Dictionary<string, string[]>
            {
                ["quantities"] = new[] { "You must specify at least one card in the maindeck or sideboard." }
            };
            return Results.BadRequest(new { errors = quantityErrors });
        }

        var deckCard = await deckCardService.CreateDeckCardAsync(deckId, createDto);
        return Results.Created($"/api/decks/{deckId}/cards/{deckCard.Id}", deckCard);
    }

    private static async Task<IResult> UpdateDeckCardAsync(
        int deckId,
        int id,
        UpdateDeckCardDto updateDto,
        DeckCardService deckCardService,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Results.Unauthorized();

        // Verify deck ownership
        var deck = await unitOfWork.Repository<Deck>().GetByIdAsync(deckId);
        if (deck == null || deck.OwnerId != userId) return Results.NotFound();

        // Verify deck card belongs to this deck
        var existingDeckCard = await deckCardService.GetDeckCardByIdAsync(id);
        if (existingDeckCard == null || existingDeckCard.DeckId != deckId) return Results.NotFound();

        var updatedDeckCard = await deckCardService.UpdateDeckCardAsync(id, updateDto);
        return Results.Ok(updatedDeckCard);
    }

    private static async Task<IResult> DeleteDeckCardAsync(
        int deckId,
        int id,
        DeckCardService deckCardService,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Results.Unauthorized();

        // Verify deck ownership
        var deck = await unitOfWork.Repository<Deck>().GetByIdAsync(deckId);
        if (deck == null || deck.OwnerId != userId) return Results.NotFound();

        // Verify deck card belongs to this deck
        var existingDeckCard = await deckCardService.GetDeckCardByIdAsync(id);
        if (existingDeckCard == null || existingDeckCard.DeckId != deckId) return Results.NotFound();

        var deleted = await deckCardService.DeleteDeckCardAsync(id);
        return deleted ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> CreateDeckAsync(
        CreateDeckDto createDto,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Results.Unauthorized();

        var deck = new Deck
        {
            Name = createDto.Name,
            Format = createDto.Format,
            OwnerId = userId,
            NumberOfCards = 0,
            TotalPrice = 0.0
        };

        unitOfWork.Repository<Deck>().Add(deck);
        await unitOfWork.Complete();

        return Results.Created($"/api/decks/{deck.Id}", MapToDto(deck));
    }

    private static async Task<IResult> GetDeckByIdAsync(
        int id,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Results.Unauthorized();

        var deck = await unitOfWork.Repository<Deck>().GetByIdAsync(id);
        if (deck == null || deck.OwnerId != userId) return Results.NotFound();

        return Results.Ok(MapToDto(deck));
    }

    private static async Task<IResult> UpdateDeckAsync(
        int id,
        UpdateDeckDto updateDto,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Results.Unauthorized();

        var deck = await unitOfWork.Repository<Deck>().GetByIdAsync(id);
        if (deck == null || deck.OwnerId != userId) return Results.NotFound();

        deck.Name = updateDto.Name;
        deck.Format = updateDto.Format;

        unitOfWork.Repository<Deck>().Update(deck);
        await unitOfWork.Complete();

        return Results.Ok(MapToDto(deck));
    }

    private static async Task<IResult> DeleteDeckAsync(
        int id,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Results.Unauthorized();

        var deck = await unitOfWork.Repository<Deck>().GetByIdAsync(id);
        if (deck == null || deck.OwnerId != userId) return Results.NotFound();

        // Delete all deck cards first
        var deckCardsSpec = new DeckCardsWithDeckIdSpecification(id);
        var deckCards = await unitOfWork.Repository<DeckCard>().ListAsync(deckCardsSpec);
        if (deckCards?.Any() == true)
        {
            foreach (var deckCard in deckCards)
            {
                unitOfWork.Repository<DeckCard>().Delete(deckCard);
            }
        }

        // Delete the deck
        unitOfWork.Repository<Deck>().Delete(deck);
        await unitOfWork.Complete();

        return Results.NoContent();
    }

    private static DeckDto MapToDto(Deck deck)
    {
        return new DeckDto
        {
            Id = deck.Id,
            Name = deck.Name,
            Format = deck.Format,
            NumberOfCards = deck.NumberOfCards,
            TotalPrice = deck.TotalPrice,
            OwnerId = deck.OwnerId
        };
    }
}
