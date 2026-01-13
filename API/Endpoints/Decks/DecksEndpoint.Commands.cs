using System.Security.Claims;
using API.Dtos.Decks;
using API.Logging;
using API.Services;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints.Decks;

public static partial class DecksEndpoint
{
    private static async Task<IResult> CreateDeckAsync(
        CreateDeckDto createDto,
        HttpContext context,
        [FromServices] IDeckService deckService,
        [FromServices] ILogger<DecksEndpointLogCategory> logger,
        CancellationToken cancellationToken)
    {
        const string operation = "Decks.Create";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null)
        {
            logger.LogOperationWarning(operation, "Missing user identifier");
            return Results.Unauthorized();
        }

        using var scope = logger.BeginOperationScope(operation, userId);
        logger.LogOperationStart(operation, new { createDto.Name, createDto.Format });

        try
        {
            var deck = await deckService.CreateDeckAsync(createDto, userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { deck.Id });
            return Results.Ok(deck);
        }
        catch (DeckServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { userId });
            return MapDeckServiceException(ex);
        }
    }

    private static async Task<IResult> UpdateDeckAsync(
        int id,
        UpdateDeckDto updateDto,
        HttpContext context,
        [FromServices] IDeckService deckService,
        [FromServices] ILogger<DecksEndpointLogCategory> logger,
        CancellationToken cancellationToken)
    {
        const string operation = "Decks.Update";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null)
        {
            logger.LogOperationWarning(operation, "Missing user identifier");
            return Results.Unauthorized();
        }

        using var scope = logger.BeginOperationScope(operation, id);
        logger.LogOperationStart(operation, new { id, updateDto.Name, updateDto.Format });

        try
        {
            var deck = await deckService.UpdateDeckAsync(id, updateDto, userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { id });
            return Results.Ok(deck);
        }
        catch (DeckServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { id, userId });
            return MapDeckServiceException(ex);
        }
    }

    private static async Task<IResult> DeleteDeckAsync(
        int id,
        HttpContext context,
        [FromServices] IDeckService deckService,
        [FromServices] ILogger<DecksEndpointLogCategory> logger,
        CancellationToken cancellationToken)
    {
        const string operation = "Decks.Delete";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null)
        {
            logger.LogOperationWarning(operation, "Missing user identifier");
            return Results.Unauthorized();
        }

        using var scope = logger.BeginOperationScope(operation, id);
        logger.LogOperationStart(operation, new { id });

        try
        {
            await deckService.DeleteDeckAsync(id, userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { id });
            return Results.NoContent();
        }
        catch (DeckServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { id, userId });
            return MapDeckServiceException(ex);
        }
    }

    private static async Task<IResult> CreateDeckCardAsync(
        int deckId,
        CreateDeckCardDto createDto,
        HttpContext context,
        [FromServices] IDeckService deckService,
        [FromServices] ILogger<DecksEndpointLogCategory> logger,
        CancellationToken cancellationToken)
    {
        const string operation = "DeckCards.Create";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null)
        {
            logger.LogOperationWarning(operation, "Missing user identifier", new { deckId });
            return Results.Unauthorized();
        }

        using var scope = logger.BeginOperationScope(operation, deckId);
        logger.LogOperationStart(operation, new { deckId, createDto.Name });

        try
        {
            var createdDeckCard = await deckService.CreateDeckCardAsync(deckId, createDto, userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { deckId, createdDeckCard.Id });
            return Results.Ok(createdDeckCard);
        }
        catch (DeckServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { deckId, userId });
            return MapDeckServiceException(ex);
        }
    }

    private static async Task<IResult> UpdateDeckCardAsync(
        int deckId,
        int id,
        UpdateDeckCardDto updateDto,
        HttpContext context,
        [FromServices] IDeckService deckService,
        [FromServices] ILogger<DecksEndpointLogCategory> logger,
        CancellationToken cancellationToken)
    {
        const string operation = "DeckCards.Update";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null)
        {
            logger.LogOperationWarning(operation, "Missing user identifier", new { deckId, id });
            return Results.Unauthorized();
        }

        using var scope = logger.BeginOperationScope(operation, id);
        logger.LogOperationStart(operation, new { deckId, id });

        try
        {
            var updatedDeckCard = await deckService.UpdateDeckCardAsync(deckId, id, updateDto, userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { deckId, id });
            return Results.Ok(updatedDeckCard);
        }
        catch (DeckServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { deckId, id, userId });
            return MapDeckServiceException(ex);
        }
    }

    private static async Task<IResult> UpdateDeckCardVersionAsync(
        int deckId,
        int id,
        UpdateDeckCardVersionDto updateDto,
        HttpContext context,
        [FromServices] IDeckService deckService,
        [FromServices] ILogger<DecksEndpointLogCategory> logger,
        CancellationToken cancellationToken)
    {
        const string operation = "DeckCards.UpdateVersion";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null)
        {
            logger.LogOperationWarning(operation, "Missing user identifier", new { deckId, id });
            return Results.Unauthorized();
        }

        using var scope = logger.BeginOperationScope(operation, id);
        logger.LogOperationStart(operation, new { deckId, id, updateDto.ScryfallId });

        try
        {
            var updatedDeckCard = await deckService.UpdateDeckCardVersionAsync(deckId, id, updateDto, userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { deckId, id });
            return Results.Ok(updatedDeckCard);
        }
        catch (DeckServiceException ex)
        {
            if (ex.StatusCode == StatusCodes.Status500InternalServerError)
            {
                logger.LogOperationFailure(operation, ex, new { deckId, id });
            }
            else
            {
                logger.LogOperationWarning(operation, ex.Message, new { deckId, id, userId });
            }
            return MapDeckServiceException(ex);
        }
    }

    private static async Task<IResult> DeleteDeckCardAsync(
        int deckId,
        int id,
        HttpContext context,
        [FromServices] IDeckService deckService,
        [FromServices] ILogger<DecksEndpointLogCategory> logger,
        CancellationToken cancellationToken)
    {
        const string operation = "DeckCards.Delete";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null)
        {
            logger.LogOperationWarning(operation, "Missing user identifier", new { deckId, id });
            return Results.Unauthorized();
        }

        using var scope = logger.BeginOperationScope(operation, id);
        logger.LogOperationStart(operation, new { deckId, id });

        try
        {
            await deckService.DeleteDeckCardAsync(deckId, id, userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { deckId, id });
            return Results.NoContent();
        }
        catch (DeckServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { deckId, id, userId });
            return MapDeckServiceException(ex);
        }
    }

    private static async Task<IResult> ImportDeckFromDecklistAsync(
        DeckImportRequestDto importDto,
        HttpContext context,
        [FromServices] IDeckService deckService,
        [FromServices] ILogger<DecksEndpointLogCategory> logger,
        CancellationToken cancellationToken)
    {
        const string operation = "Decks.Import";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null)
        {
            logger.LogOperationWarning(operation, "Missing user identifier", new { importDto.Name });
            return Results.Unauthorized();
        }

        using var scope = logger.BeginOperationScope(operation, userId);
        logger.LogOperationStart(operation, new { importDto.Name, importDto.Format });

        try
        {
            var response = await deckService.ImportDeckFromDecklistAsync(importDto, userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { response.Deck.Id, createdCards = response.DeckCards.Count, response.SkippedLines });
            return Results.Created($"/api/decks/{response.Deck.Id}", response);
        }
        catch (DeckServiceException ex)
        {
            if (ex.StatusCode == StatusCodes.Status400BadRequest)
            {
                logger.LogOperationWarning(operation, ex.Message, new { userId, ex.Body });
            }
            return MapDeckServiceException(ex);
        }
    }
}
