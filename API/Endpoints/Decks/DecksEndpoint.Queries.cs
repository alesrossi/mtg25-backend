using System.Security.Claims;
using API.Dtos.Decks;
using API.Logging;
using API.Services;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints.Decks;

public static partial class DecksEndpoint
{
    private static async Task<IResult> GetAllDecksForUser(
        HttpContext context,
        [FromServices] IDeckService deckService,
        [FromServices] ILogger<DecksEndpointLogCategory> logger,
        CancellationToken cancellationToken)
    {
        const string operation = "Decks.List";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user identifier");
            return Results.Unauthorized();
        }

        using var scope = logger.BeginOperationScope(operation, userId);
        logger.LogOperationStart(operation, new { userId });

        try
        {
            var deckDtos = await deckService.GetDecksForUserAsync(userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { Count = deckDtos.Count });
            return Results.Ok(deckDtos);
        }
        catch (DeckServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { userId });
            return MapDeckServiceException(ex);
        }
    }

    private static async Task<IResult> GetDeckByIdAsync(
        int id,
        HttpContext context,
        [FromServices] IDeckService deckService,
        [FromServices] ILogger<DecksEndpointLogCategory> logger,
        CancellationToken cancellationToken)
    {
        const string operation = "Decks.Get";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null)
        {
            logger.LogOperationWarning(operation, "Missing user identifier", new { id });
            return Results.Unauthorized();
        }

        using var scope = logger.BeginOperationScope(operation, id);
        logger.LogOperationStart(operation, new { id });

        try
        {
            var deck = await deckService.GetDeckByIdAsync(id, userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { id });
            return Results.Ok(deck);
        }
        catch (DeckServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { id, userId });
            return MapDeckServiceException(ex);
        }
    }

    private static async Task<IResult> GetDeckCardsAsync(
        int deckId,
        bool maindeckOnly = false,
        bool sideboardOnly = false,
        bool? ownedOnly = null,
        HttpContext context = null!,
        [FromServices] IDeckService deckService = null!,
        [FromServices] ILogger<DecksEndpointLogCategory> logger = null!,
        CancellationToken cancellationToken = default)
    {
        const string operation = "DeckCards.Query";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null)
        {
            logger.LogOperationWarning(operation, "Missing user identifier", new { deckId });
            return Results.Unauthorized();
        }

        using var scope = logger.BeginOperationScope(operation, deckId);
        logger.LogOperationStart(operation, new { deckId, maindeckOnly, sideboardOnly, ownedOnly });

        try
        {
            var deckCards = await deckService.GetDeckCardsAsync(deckId, userId, maindeckOnly, sideboardOnly, ownedOnly, cancellationToken);
            logger.LogOperationSuccess(operation, new { deckId, Count = deckCards.Count });
            return Results.Ok(deckCards);
        }
        catch (DeckServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { deckId, userId });
            return MapDeckServiceException(ex);
        }
    }

    private static async Task<IResult> GetDeckCardByIdAsync(
        int deckId,
        int id,
        HttpContext context,
        [FromServices] IDeckService deckService,
        [FromServices] ILogger<DecksEndpointLogCategory> logger,
        CancellationToken cancellationToken)
    {
        const string operation = "DeckCards.Get";
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
            var deckCard = await deckService.GetDeckCardByIdAsync(deckId, id, userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { deckId, id });
            return Results.Ok(deckCard);
        }
        catch (DeckServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { deckId, id, userId });
            return MapDeckServiceException(ex);
        }
    }

    private static async Task<IResult> GetMissingDeckCardsAsync(
        int deckId,
        HttpContext context,
        [FromServices] IDeckService deckService,
        [FromServices] ILogger<DecksEndpointLogCategory> logger,
        CancellationToken cancellationToken)
    {
        const string operation = "DeckCards.Missing";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null)
        {
            logger.LogOperationWarning(operation, "Missing user identifier", new { deckId });
            return Results.Unauthorized();
        }

        using var scope = logger.BeginOperationScope(operation, deckId);
        logger.LogOperationStart(operation, new { deckId });

        try
        {
            var missingCards = await deckService.GetMissingDeckCardsAsync(deckId, userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { deckId, MissingCount = missingCards.Count });
            return Results.Ok(missingCards);
        }
        catch (DeckServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { deckId, userId });
            return MapDeckServiceException(ex);
        }
    }

    private static async Task<IResult> ExportDeckAsync(
        int deckId,
        HttpContext context,
        [FromServices] IDeckService deckService,
        [FromServices] ILogger<DecksEndpointLogCategory> logger,
        CancellationToken cancellationToken)
    {
        const string operation = "Decks.Export";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null)
        {
            logger.LogOperationWarning(operation, "Missing user identifier", new { deckId });
            return Results.Unauthorized();
        }

        using var scope = logger.BeginOperationScope(operation, deckId);
        logger.LogOperationStart(operation, new { deckId });

        try
        {
            var exportedLines = await deckService.ExportDeckAsync(deckId, userId, cancellationToken);
            if (exportedLines.Count == 0)
            {
                logger.LogOperationWarning(operation, "Deck has no cards", new { deckId });
            }
            logger.LogOperationSuccess(operation, new { deckId, Lines = exportedLines.Count });
            return Results.Ok(exportedLines);
        }
        catch (DeckServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { deckId, userId });
            return MapDeckServiceException(ex);
        }
    }
}
