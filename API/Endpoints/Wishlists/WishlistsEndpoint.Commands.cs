using System.Security.Claims;
using API.Dtos.Wishlists;
using API.Logging;
using API.Services;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints.Wishlists;

public static partial class WishlistsEndpoint
{
    private static async Task<IResult> CreateWishlistAsync(
        CreateWishlistDto createDto,
        HttpContext context,
        [FromServices] IWishlistService wishlistService,
        [FromServices] IMessageLocalizer messageLocalizer,
        [FromServices] ILogger<WishlistsEndpointLogCategory> logger,
        CancellationToken cancellationToken)
    {
        const string operation = "Wishlists.Create";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            logger.LogOperationWarning(operation, "Missing user id");
            return Results.Unauthorized();
        }

        try
        {
            var wishlist = await wishlistService.CreateWishlistAsync(createDto, userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { wishlist.Id });
            return Results.Ok(wishlist);
        }
        catch (WishlistServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { userId });
            return await MapWishlistServiceException(ex, context, messageLocalizer, userId);
        }
    }

    private static async Task<IResult> UpdateWishlistAsync(
        int id,
        UpdateWishlistDto updateDto,
        HttpContext context,
        [FromServices] IWishlistService wishlistService,
        [FromServices] IMessageLocalizer messageLocalizer,
        [FromServices] ILogger<WishlistsEndpointLogCategory> logger,
        CancellationToken cancellationToken)
    {
        const string operation = "Wishlists.Update";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            logger.LogOperationWarning(operation, "Missing user id", new { id });
            return Results.Unauthorized();
        }

        try
        {
            var wishlist = await wishlistService.UpdateWishlistAsync(id, updateDto, userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { id });
            return Results.Ok(wishlist);
        }
        catch (WishlistServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { id, userId });
            return await MapWishlistServiceException(ex, context, messageLocalizer, userId);
        }
    }

    private static async Task<IResult> DeleteWishlistAsync(
        int id,
        HttpContext context,
        [FromServices] IWishlistService wishlistService,
        [FromServices] IMessageLocalizer messageLocalizer,
        [FromServices] ILogger<WishlistsEndpointLogCategory> logger,
        CancellationToken cancellationToken)
    {
        const string operation = "Wishlists.Delete";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            logger.LogOperationWarning(operation, "Missing user id", new { id });
            return Results.Unauthorized();
        }

        try
        {
            await wishlistService.DeleteWishlistAsync(id, userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { id });
            return Results.NoContent();
        }
        catch (WishlistServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { id, userId });
            return await MapWishlistServiceException(ex, context, messageLocalizer, userId);
        }
    }

    private static async Task<IResult> CreateWishlistCardAsync(
        int wishlistId,
        List<CreateWishlistCardDto> newWishlistCardList,
        HttpContext context,
        [FromServices] IWishlistService wishlistService,
        [FromServices] IMessageLocalizer messageLocalizer,
        [FromServices] ILogger<WishlistsEndpointLogCategory> logger,
        CancellationToken cancellationToken)
    {
        const string operation = "Wishlists.Cards.Create";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            logger.LogOperationWarning(operation, "Missing user id", new { wishlistId });
            return Results.Unauthorized();
        }

        try
        {
            var cardList = await wishlistService.CreateWishlistCardsAsync(wishlistId, newWishlistCardList, userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { wishlistId, Added = cardList.Count });
            return Results.Ok(cardList);
        }
        catch (WishlistServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { wishlistId });
            return await MapWishlistServiceException(ex, context, messageLocalizer, userId);
        }
    }

    private static async Task<IResult> UpdateWishlistCardAsync(
        int wishlistId,
        int cardId,
        UpdateWishlistCardDto updateDto,
        HttpContext context,
        [FromServices] IWishlistService wishlistService,
        [FromServices] IMessageLocalizer messageLocalizer,
        [FromServices] ILogger<WishlistsEndpointLogCategory> logger,
        CancellationToken cancellationToken)
    {
        const string operation = "Wishlists.Cards.Update";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            logger.LogOperationWarning(operation, "Missing user id", new { wishlistId, cardId });
            return Results.Unauthorized();
        }

        try
        {
            var wishlistCard = await wishlistService.UpdateWishlistCardAsync(wishlistId, cardId, updateDto, userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { wishlistId, cardId });
            return Results.Ok(wishlistCard);
        }
        catch (WishlistServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { wishlistId, cardId });
            return await MapWishlistServiceException(ex, context, messageLocalizer, userId);
        }
    }

    private static async Task<IResult> DeleteWishlistCardAsync(
        int wishlistId,
        int cardId,
        HttpContext context,
        [FromServices] IWishlistService wishlistService,
        [FromServices] IMessageLocalizer messageLocalizer,
        [FromServices] ILogger<WishlistsEndpointLogCategory> logger,
        CancellationToken cancellationToken)
    {
        const string operation = "Wishlists.Cards.Delete";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            logger.LogOperationWarning(operation, "Missing user id", new { wishlistId, cardId });
            return Results.Unauthorized();
        }

        try
        {
            await wishlistService.DeleteWishlistCardAsync(wishlistId, cardId, userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { wishlistId, cardId });
            return Results.NoContent();
        }
        catch (WishlistServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { wishlistId, cardId });
            return await MapWishlistServiceException(ex, context, messageLocalizer, userId);
        }
    }
}
