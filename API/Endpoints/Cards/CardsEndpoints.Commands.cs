using System.Security.Claims;
using API.Dtos.Cards;
using API.Helpers;
using API.Logging;
using API.Services;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints.Cards;

public static partial class CardsEndpoints
{
    private static async Task<IResult> UpdateCardFromIdAsync(
        int id,
        HttpContext context,
        [FromBody] UpdateCollectionCardDto updateDto,
        [FromServices] ICardsService cardsService,
        [FromServices] ILogger<CardsEndpointsLogCategory> logger)
    {
        const string operation = "Cards.Update";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id", new { id });
            return ProblemResultFactory.Create(
                context,
                StatusCodes.Status401Unauthorized,
                "Authentication required",
                "You must be logged in to update cards.",
                "card-update-auth-required");
        }

        logger.LogOperationStart(operation, new { id });

        try
        {
            var card = await cardsService.UpdateCardAsync(id, updateDto, userId);
            logger.LogOperationSuccess(operation, new { id });
            return Results.Ok(card);
        }
        catch (CardsServiceException ex)
        {
            logger.LogOperationFailure(operation, ex, new { id });
            return MapCardsServiceException(ex, context);
        }
    }
    
    private static async Task<IResult> UpdateCardVersionFromIdAsync(
        int id,
        HttpContext context,
        [FromBody] UpdateCollectionCardWithSFIdDto updateDto,
        [FromServices] ICardsService cardsService,
        [FromServices] ILogger<CardsEndpointsLogCategory> logger)
    {
        const string operation = "Cards.Update";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id", new { id });
            return ProblemResultFactory.Create(
                context,
                StatusCodes.Status401Unauthorized,
                "Authentication required",
                "You must be logged in to update cards.",
                "card-update-auth-required");
        }

        logger.LogOperationStart(operation, new { id });

        try
        {
            var card = await cardsService.UpdateCardVersionAsync(id, updateDto, userId);
            logger.LogOperationSuccess(operation, new { id });
            return Results.Ok(card);
        }
        catch (CardsServiceException ex)
        {
            logger.LogOperationFailure(operation, ex, new { id });
            return MapCardsServiceException(ex, context);
        }
    }

    private static async Task<IResult> DeleteCardFromIdAsync(
        int id,
        HttpContext context,
        [FromServices] ICardsService cardsService,
        [FromServices] ILogger<CardsEndpointsLogCategory> logger)
    {
        const string operation = "Cards.Delete";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id", new { id });
            return ProblemResultFactory.Create(
                context,
                StatusCodes.Status401Unauthorized,
                "Authentication required",
                "You must be logged in to delete cards.",
                "card-delete-auth-required");
        }

        try
        {
            logger.LogOperationStart(operation, new { id });

            await cardsService.DeleteCardAsync(id, userId);
            logger.LogOperationSuccess(operation, new { id });
            return Results.NoContent();
        }
        catch (CardsServiceException ex)
        {
            logger.LogOperationFailure(operation, ex, new { id });
            return MapCardsServiceException(ex, context);
        }
    }

    private static async Task<IResult> AddNewCardAsync(
        HttpContext context,
        InternalCardDto cardDto,
        [FromServices] ICardsService cardsService,
        [FromServices] ILogger<CardsEndpointsLogCategory> logger)
    {
        const string operation = "Cards.AddInternal";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id");
            return Results.Unauthorized();
        }

        try
        {
            var card = await cardsService.AddNewCardAsync(cardDto, userId);
            logger.LogOperationSuccess(operation, new { card.Id, card.CollectionId });
            return Results.Ok(card);
        }
        catch (CardsServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { cardDto.CollectionId, userId });
            return MapCardsServiceException(ex, context);
        }
    }

    private static async Task<IResult> AddCardListAsync(
        CardListDto cardListDto,
        HttpContext context,
        [FromServices] ICardsService cardsService,
        [FromServices] ILogger<CardsEndpointsLogCategory> logger)
    {
        const string operation = "Cards.AddList";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id");
            return Results.Unauthorized();
        }

        try
        {
            var scryfallCardList = await cardsService.AddCardListAsync(cardListDto, userId);
            logger.LogOperationSuccess(operation, new { Count = scryfallCardList.Count });
            return Results.Ok(scryfallCardList);
        }
        catch (CardsServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { userId });
            return MapCardsServiceException(ex, context);
        }
    }
}
