using System.Security.Claims;
using API.Dtos.Cards;
using API.Logging;
using API.Services;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints.Cards;

public static partial class CardsEndpoints
{
    private static async Task<IResult> GetCardFromId(
        int id,
        HttpContext context,
        [FromServices] ICardsService cardsService,
        [FromServices] IMessageLocalizer messageLocalizer,
        [FromServices] ILogger<CardsEndpointsLogCategory> logger,
        CancellationToken cancellationToken)
    {
        const string operation = "Cards.GetById";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id", new { id });
            return Results.Unauthorized();
        }

        try
        {
            var card = await cardsService.GetCardByIdAsync(id, userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { id });
            return Results.Ok(card);
        }
        catch (CardsServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { id, userId });
            return await MapCardsServiceException(ex, context, messageLocalizer, userId);
        }
    }

    private static async Task<IResult> SearchCards(
        string find,
        HttpContext context,
        [FromServices] ICardsService cardsService,
        [FromServices] IMessageLocalizer messageLocalizer,
        [FromServices] ILogger<CardsEndpointsLogCategory> logger)
    {
        const string operation = "Cards.Search";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id");
            return Results.Unauthorized();
        }

        try
        {
            var result = await cardsService.SearchCardsAsync(find, userId);
            logger.LogOperationSuccess(operation, new { find, Count = result.Count });
            return Results.Ok(result);
        }
        catch (CardsServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { find, userId });
            return await MapCardsServiceException(ex, context, messageLocalizer, userId);
        }
    }

    private static async Task<IResult> GetCardVersionsAsync(
        string name,
        HttpContext context,
        [FromServices] ICardsService cardsService,
        [FromServices] IMessageLocalizer messageLocalizer,
        [FromServices] ILogger<CardsEndpointsLogCategory> logger)
    {
        const string operation = "Cards.Versions";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id", new { name });
            return Results.Unauthorized();
        }

        try
        {
            var versions = await cardsService.GetCardVersionsAsync(name, userId);
            logger.LogOperationSuccess(operation, new { name, Count = versions.Count });
            return Results.Ok(versions);
        }
        catch (CardsServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { name, userId });
            return await MapCardsServiceException(ex, context, messageLocalizer, userId);
        }
    }

    private static async Task<IResult> GetCardFromExactName(
        string name,
        HttpContext context,
        [FromServices] ICardsService cardsService,
        [FromServices] IMessageLocalizer messageLocalizer,
        [FromServices] ILogger<CardsEndpointsLogCategory> logger)
    {
        const string operation = "Cards.ScryfallByName";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id", new { name });
            return Results.Unauthorized();
        }

        try
        {
            var card = await cardsService.GetCardFromExactNameAsync(name, userId);
            logger.LogOperationSuccess(operation, new { name });
            return Results.Ok(card);
        }
        catch (CardsServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { name, userId });
            return await MapCardsServiceException(ex, context, messageLocalizer, userId);
        }
    }

    private static async Task<IResult> GetCardFromScryfallId(
        string id,
        HttpContext context,
        [FromServices] ICardsService cardsService,
        [FromServices] IMessageLocalizer messageLocalizer,
        [FromServices] ILogger<CardsEndpointsLogCategory> logger)
    {
        const string operation = "Cards.ScryfallById";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id", new { id });
            return Results.Unauthorized();
        }

        try
        {
            var card = await cardsService.GetCardFromScryfallIdAsync(id, userId);
            logger.LogOperationSuccess(operation, new { id });
            return Results.Ok(card);
        }
        catch (CardsServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { id, userId });
            return await MapCardsServiceException(ex, context, messageLocalizer, userId);
        }
    }
}
