using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using API.Dtos.Decks;
using API.Services;
using Core.Interfaces;
using Core.Models;
using Core.Specifications;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace API.Endpoints;

public static partial class DecksEndpoint
{
    private static void MapDeckCommands(RouteGroupBuilder group)
    {
        group.MapPost("/", CreateDeckAsync)
            .RequireAuthorization()
            .WithSummary("Create new deck")
            .WithDescription("Creates a new deck for the authenticated user")
            .Produces<DeckDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapPut("/{id:int}", UpdateDeckAsync)
            .RequireAuthorization()
            .WithSummary("Update deck")
            .WithDescription("Updates deck metadata (name, format)")
            .Produces<DeckDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:int}", DeleteDeckAsync)
            .RequireAuthorization()
            .WithSummary("Delete deck")
            .WithDescription("Deletes a deck and all its cards")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{deckId:int}/cards", CreateDeckCardAsync)
            .RequireAuthorization()
            .WithSummary("Add card to deck")
            .WithDescription("Adds a new card to the deck")
            .Produces<DeckCardDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPut("/{deckId:int}/cards/{id:int}", UpdateDeckCardAsync)
            .RequireAuthorization()
            .WithSummary("Update deck card")
            .WithDescription("Updates deck card quantities and owned card reference")
            .Produces<DeckCardDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/{deckId:int}/cards/{id:int}", DeleteDeckCardAsync)
            .RequireAuthorization()
            .WithSummary("Remove card from deck")
            .WithDescription("Removes a card from the deck")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/import", ImportDeckFromDecklistAsync)
            .RequireAuthorization()
            .WithSummary("Import deck from text decklist")
            .WithDescription("Parses a decklist and creates a new deck with the imported cards")
            .Produces<ImportDeckDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);
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
        deck.Image = updateDto.Image;

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

        var deckCardsSpec = new DeckCardsWithDeckIdSpecification(id);
        var deckCards = await unitOfWork.Repository<DeckCard>().ListAsync(deckCardsSpec);
        if (deckCards?.Any() == true)
        {
            foreach (var deckCard in deckCards)
            {
                unitOfWork.Repository<DeckCard>().Delete(deckCard);
            }
        }

        unitOfWork.Repository<Deck>().Delete(deck);
        await unitOfWork.Complete();

        return Results.NoContent();
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

        var deck = await unitOfWork.Repository<Deck>().GetByIdAsync(deckId);
        if (deck == null || deck.OwnerId != userId) return Results.NotFound();

        var (isValid, errors) = validationService.ValidateModel(createDto);
        if (!isValid || createDto.MaindeckQuantity + createDto.SideboardQuantity == 0)
        {
            return Results.BadRequest(new { errors });
        }

        var createdDeckCard = await deckCardService.CreateDeckCardAsync(deckId, createDto);
        return Results.Ok(createdDeckCard);
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

        var deck = await unitOfWork.Repository<Deck>().GetByIdAsync(deckId);
        if (deck == null || deck.OwnerId != userId) return Results.NotFound();

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

        var deck = await unitOfWork.Repository<Deck>().GetByIdAsync(deckId);
        if (deck == null || deck.OwnerId != userId) return Results.NotFound();

        var existingDeckCard = await deckCardService.GetDeckCardByIdAsync(id);
        if (existingDeckCard == null || existingDeckCard.DeckId != deckId) return Results.NotFound();

        var deleted = await deckCardService.DeleteDeckCardAsync(id);
        return deleted ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> ImportDeckFromDecklistAsync(
        DeckImportRequestDto importDto,
        IDecklistParserService decklistParserService,
        DeckCardService deckCardService,
        [FromServices] IValidationService validationService,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Results.Unauthorized();

        var (isValid, validationErrors) = validationService.ValidateModel(importDto);
        if (!isValid)
        {
            return Results.BadRequest(new { errors = validationErrors });
        }

        var decklistLines = FilterDecklistLines(importDto.Decklist);

        var parseResult = await decklistParserService.ParseAsync(decklistLines);

        var skippedLines = parseResult.Errors.Count;

        if (!parseResult.DeckCards.Any())
        {
            var errors = parseResult.Errors.Any()
                ? parseResult.Errors
                : new[] { "Decklist did not contain any valid cards." };

            return Results.BadRequest(new
            {
                errors,
                deckCards = parseResult.DeckCards,
                skippedLines
            });
        }

        var deck = new Deck
        {
            Name = importDto.Name.Trim(),
            Format = importDto.Format.Trim(),
            OwnerId = userId,
            NumberOfCards = 0,
            TotalPrice = 0
        };

        unitOfWork.Repository<Deck>().Add(deck);
        await unitOfWork.Complete();

        var createdCards = new List<DeckCardDto>();
        foreach (var deckCardDto in parseResult.DeckCards)
        {
            var created = await deckCardService.CreateDeckCardAsync(deck.Id, deckCardDto);
            deck.ColorIdentity.AddRange(created.ColorIdentity.Except(deck.ColorIdentity));
            createdCards.Add(created);
        }

        deck.NumberOfCards = createdCards.Sum(dc => dc.MaindeckQuantity + dc.SideboardQuantity);
        deck.NumberOfMainBoardCards = createdCards.Sum(dc => dc.MaindeckQuantity);
        deck.NumberOfSideBoardCards = createdCards.Sum(dc => dc.SideboardQuantity);
        unitOfWork.Repository<Deck>().Update(deck);
        await unitOfWork.Complete();

        return Results.Created($"/api/decks/{deck.Id}", new ImportDeckDto(MapToDto(deck), createdCards, parseResult.Errors, skippedLines));
    }

    private static string[] FilterDecklistLines(string decklist)
    {
        var rawLines = decklist
            .Replace("\r", string.Empty)
            .Split('\n', StringSplitOptions.None);

        var filteredLines = new List<string>();
        var dividerAdded = false;

        foreach (var rawLine in rawLines)
        {
            var trimmed = rawLine.Trim();

            if (string.IsNullOrEmpty(trimmed))
            {
                if (!dividerAdded)
                {
                    filteredLines.Add(string.Empty);
                    dividerAdded = true;
                }
                continue;
            }

            if (char.IsDigit(trimmed[0]))
            {
                filteredLines.Add(trimmed);
            }
        }

        return filteredLines.ToArray();
    }
}
