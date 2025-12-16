using System;
using System.Linq;
using System.Security.Claims;
using API.Dtos.Decks;
using API.Logging;
using API.Services;
using Core.Interfaces;
using Core.Models;
using Core.Specifications;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints.Decks;

public static partial class DecksEndpoint
{
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
            .Produces<DeckDto>()
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
    }

    private static async Task<IResult> CreateDeckAsync(
        CreateDeckDto createDto,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user,
        [FromServices] ILogger<DecksEndpointLogCategory> logger)
    {
        const string operation = "Decks.Create";
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null)
        {
            logger.LogOperationWarning(operation, "Missing user identifier");
            return Results.Unauthorized();
        }

        using var scope = logger.BeginOperationScope(operation, userId);
        logger.LogOperationStart(operation, new { createDto.Name, createDto.Format });

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

        logger.LogOperationSuccess(operation, new { deck.Id });
        return Results.Ok(MapToDto(deck));
    }

    private static async Task<IResult> UpdateDeckAsync(
        int id,
        UpdateDeckDto updateDto,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user,
        [FromServices] ILogger<DecksEndpointLogCategory> logger)
    {
        const string operation = "Decks.Update";
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null)
        {
            logger.LogOperationWarning(operation, "Missing user identifier");
            return Results.Unauthorized();
        }

        using var scope = logger.BeginOperationScope(operation, id);
        logger.LogOperationStart(operation, new { id, updateDto.Name, updateDto.Format });

        var deck = await unitOfWork.Repository<Deck>().GetByIdAsync(id);
        if (deck == null || deck.OwnerId != userId)
        {
            logger.LogOperationWarning(operation, "Deck not found or unauthorized", new { id, userId });
            return Results.NotFound();
        }

        deck.Name = updateDto.Name;
        deck.Format = updateDto.Format;
        deck.Image = updateDto.Image;

        unitOfWork.Repository<Deck>().Update(deck);
        await unitOfWork.Complete();

        logger.LogOperationSuccess(operation, new { id });
        return Results.Ok(MapToDto(deck));
    }

    private static async Task<IResult> DeleteDeckAsync(
        int id,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user,
        [FromServices] ILogger<DecksEndpointLogCategory> logger)
    {
        const string operation = "Decks.Delete";
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null)
        {
            logger.LogOperationWarning(operation, "Missing user identifier");
            return Results.Unauthorized();
        }

        using var scope = logger.BeginOperationScope(operation, id);
        logger.LogOperationStart(operation, new { id });

        var deck = await unitOfWork.Repository<Deck>().GetByIdAsync(id);
        if (deck == null || deck.OwnerId != userId)
        {
            logger.LogOperationWarning(operation, "Deck not found or unauthorized", new { id, userId });
            return Results.NotFound();
        }

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

        logger.LogOperationSuccess(operation, new { id });
        return Results.NoContent();
    }

    private static async Task<IResult> CreateDeckCardAsync(
        int deckId,
        CreateDeckCardDto createDto,
        DeckCardService deckCardService,
        [FromServices] IValidationService validationService,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user,
        [FromServices] ILogger<DecksEndpointLogCategory> logger)
    {
        const string operation = "DeckCards.Create";
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null)
        {
            logger.LogOperationWarning(operation, "Missing user identifier", new { deckId });
            return Results.Unauthorized();
        }

        using var scope = logger.BeginOperationScope(operation, deckId);
        logger.LogOperationStart(operation, new { deckId, ScryfallId = createDto.ScryfallId, createDto.Name });

        var deck = await unitOfWork.Repository<Deck>().GetByIdAsync(deckId);
        if (deck == null)
        {
            logger.LogOperationWarning(operation, "Deck not found", new { deckId });
            return Results.NotFound();
        }

        if (deck.OwnerId != userId)
        {
            logger.LogOperationWarning(operation, "Deck unauthorized", new { deckId, userId });
            return Results.Unauthorized();
        }

        var (isValid, errors) = validationService.ValidateModel(createDto);
        if (!isValid || createDto.MaindeckQuantity + createDto.SideboardQuantity == 0)
        {
            logger.LogOperationWarning(operation, "Validation failed", new { deckId, errors });
            return Results.BadRequest(new { errors });
        }

        var createdDeckCard = await deckCardService.CreateDeckCardAsync(deckId, createDto);
        logger.LogOperationSuccess(operation, new { deckId, createdDeckCard.Id });
        return Results.Ok(createdDeckCard);
    }

    private static async Task<IResult> UpdateDeckCardAsync(
        int deckId,
        int id,
        UpdateDeckCardDto updateDto,
        DeckCardService deckCardService,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user,
        [FromServices] ILogger<DecksEndpointLogCategory> logger)
    {
        const string operation = "DeckCards.Update";
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null)
        {
            logger.LogOperationWarning(operation, "Missing user identifier", new { deckId, id });
            return Results.Unauthorized();
        }

        using var scope = logger.BeginOperationScope(operation, id);
        logger.LogOperationStart(operation, new { deckId, id });

        var deck = await unitOfWork.Repository<Deck>().GetByIdAsync(deckId);
        if (deck == null || deck.OwnerId != userId)
        {
            logger.LogOperationWarning(operation, "Deck not found or unauthorized", new { deckId, userId });
            return Results.NotFound();
        }

        var existingDeckCard = await deckCardService.GetDeckCardByIdAsync(id);
        if (existingDeckCard == null || existingDeckCard.DeckId != deckId)
        {
            logger.LogOperationWarning(operation, "Deck card not found or mismatched deck", new { deckId, id });
            return Results.NotFound();
        }

        var updatedDeckCard = await deckCardService.UpdateDeckCardAsync(id, updateDto);
        logger.LogOperationSuccess(operation, new { deckId, id });
        return Results.Ok(updatedDeckCard);
    }

    private static async Task<IResult> UpdateDeckCardVersionAsync(
        int deckId,
        int id,
        UpdateDeckCardVersionDto updateDto,
        CardDataService cardDataService,
        DeckCardService deckCardService,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user,
        [FromServices] ILogger<DecksEndpointLogCategory> logger)
    {
        const string operation = "DeckCards.UpdateVersion";
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null)
        {
            logger.LogOperationWarning(operation, "Missing user identifier", new { deckId, id });
            return Results.Unauthorized();
        }

        using var scope = logger.BeginOperationScope(operation, id);
        logger.LogOperationStart(operation, new { deckId, id, updateDto.ScryfallId });

        var deck = await unitOfWork.Repository<Deck>().GetByIdAsync(deckId);
        if (deck == null || deck.OwnerId != userId)
        {
            logger.LogOperationWarning(operation, "Deck not found or unauthorized", new { deckId, userId });
            return Results.NotFound();
        }

        var deckCard = await unitOfWork.Repository<DeckCard>().GetByIdAsync(id);
        if (deckCard == null || deckCard.DeckId != deckId)
        {
            logger.LogOperationWarning(operation, "Deck card not found or mismatched deck", new { deckId, id });
            return Results.NotFound();
        }

        if (!cardDataService.CardDataById.TryGetValue(updateDto.ScryfallId, out var scryfallCard))
        {
            logger.LogOperationWarning(operation, "Invalid scryfall id", new { updateDto.ScryfallId });
            return Results.BadRequest(new { errors = new[] { "Invalid Scryfall ID provided." } });
        }

        if (!string.Equals(scryfallCard.Name, deckCard.Name, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogOperationWarning(operation, "Scryfall name mismatch", new { updateDto.ScryfallId, deckCard.Name });
            return Results.BadRequest(new { errors = new[] { $"'{updateDto.ScryfallId}' is not a valid version for '{deckCard.Name}'." } });
        }

        DeckCardDto updatedDeckCard;
        try
        {
            updatedDeckCard = (await deckCardService.UpdateDeckCardVersionAsync(id, updateDto, scryfallCard, cardDataService))!;

            unitOfWork.Repository<DeckCard>().Update(deckCard);
            await unitOfWork.Complete();
        }
        catch (Exception ex)
        {
            logger.LogOperationFailure(operation, ex, new { deckId, id });
            return Results.Problem(
                detail: "An unexpected error occurred while updating the deck card.",
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Deck card update failed");
        }
        
        logger.LogOperationSuccess(operation, new { deckId, id });
        return Results.Ok(updatedDeckCard);
    }

    private static async Task<IResult> DeleteDeckCardAsync(
        int deckId,
        int id,
        DeckCardService deckCardService,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user,
        [FromServices] ILogger<DecksEndpointLogCategory> logger)
    {
        const string operation = "DeckCards.Delete";
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null)
        {
            logger.LogOperationWarning(operation, "Missing user identifier", new { deckId, id });
            return Results.Unauthorized();
        }

        using var scope = logger.BeginOperationScope(operation, id);
        logger.LogOperationStart(operation, new { deckId, id });

        var deck = await unitOfWork.Repository<Deck>().GetByIdAsync(deckId);
        if (deck == null || deck.OwnerId != userId)
        {
            logger.LogOperationWarning(operation, "Deck not found or unauthorized", new { deckId, userId });
            return Results.NotFound();
        }

        var existingDeckCard = await deckCardService.GetDeckCardByIdAsync(id);
        if (existingDeckCard == null || existingDeckCard.DeckId != deckId)
        {
            logger.LogOperationWarning(operation, "Deck card not found or mismatched deck", new { deckId, id });
            return Results.NotFound();
        }

        var deleted = await deckCardService.DeleteDeckCardAsync(id);
        if (!deleted)
        {
            logger.LogOperationWarning(operation, "Deck card deletion failed", new { deckId, id });
            return Results.NotFound();
        }

        logger.LogOperationSuccess(operation, new { deckId, id });
        return Results.NoContent();
    }

    private static async Task<IResult> ImportDeckFromDecklistAsync(
        DeckImportRequestDto importDto,
        IDecklistParserService decklistParserService,
        DeckCardService deckCardService,
        [FromServices] IValidationService validationService,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user,
        [FromServices] ILogger<DecksEndpointLogCategory> logger)
    {
        const string operation = "Decks.Import";
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null)
        {
            logger.LogOperationWarning(operation, "Missing user identifier", new { importDto.Name });
            return Results.Unauthorized();
        }

        using var scope = logger.BeginOperationScope(operation, userId);
        logger.LogOperationStart(operation, new { importDto.Name, importDto.Format });

        var (isValid, validationErrors) = validationService.ValidateModel(importDto);
        if (!isValid)
        {
            logger.LogOperationWarning(operation, "Invalid import payload", new { validationErrors });
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

            logger.LogOperationWarning(operation, "No cards parsed", new { skippedLines, parseResult.Errors });
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

        var response = new ImportDeckDto(MapToDto(deck), createdCards, parseResult.Errors, skippedLines);
        logger.LogOperationSuccess(operation, new { deck.Id, createdCards = createdCards.Count, skippedLines });
        return Results.Created($"/api/decks/{deck.Id}", response);
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
