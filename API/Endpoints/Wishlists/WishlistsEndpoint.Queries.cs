using System.Security.Claims;
using API.Logging;
using API.Services;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints.Wishlists;

public static partial class WishlistsEndpoint
{
    private static async Task<IResult> GetWishlistsAsync(
        HttpContext context,
        [FromServices] IWishlistService wishlistService,
        [FromServices] IMessageLocalizer messageLocalizer,
        [FromServices] ILogger<WishlistsEndpointLogCategory> logger,
        CancellationToken cancellationToken)
    {
        const string operation = "Wishlists.QueryAll";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            logger.LogOperationWarning(operation, "Missing user id");
            return Results.Unauthorized();
        }

        try
        {
            var dto = await wishlistService.GetWishlistsAsync(userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { userId, dto.Count });
            return Results.Ok(dto);
        }
        catch (WishlistServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { userId });
            return await MapWishlistServiceException(ex, context, messageLocalizer, userId);
        }
    }

    private static async Task<IResult> GetWishlistByIdAsync(
        int id,
        HttpContext context,
        [FromServices] IWishlistService wishlistService,
        [FromServices] IMessageLocalizer messageLocalizer,
        [FromServices] ILogger<WishlistsEndpointLogCategory> logger,
        CancellationToken cancellationToken)
    {
        const string operation = "Wishlists.Get";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

        try
        {
            var wishlist = await wishlistService.GetWishlistByIdAsync(id, userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { id, IsAnonymous = string.IsNullOrEmpty(userId) });
            return Results.Ok(wishlist);
        }
        catch (WishlistServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { id });
            return await MapWishlistServiceException(ex, context, messageLocalizer, userId);
        }
    }

    private static async Task<IResult> GetWishlistCardsAsync(
        int wishlistId,
        HttpContext context,
        [FromServices] IWishlistService wishlistService,
        [FromServices] IMessageLocalizer messageLocalizer,
        [FromServices] ILogger<WishlistsEndpointLogCategory> logger,
        CancellationToken cancellationToken)
    {
        const string operation = "Wishlists.Cards.Query";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

        try
        {
            var cards = await wishlistService.GetWishlistCardsAsync(wishlistId, userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { wishlistId, cards.Count, IsAnonymous = string.IsNullOrEmpty(userId) });
            return Results.Ok(cards);
        }
        catch (WishlistServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { wishlistId });
            return await MapWishlistServiceException(ex, context, messageLocalizer, userId);
        }
    }

    private static async Task<IResult> GetWishlistCardByIdAsync(
        int wishlistId,
        int cardId,
        HttpContext context,
        [FromServices] IWishlistService wishlistService,
        [FromServices] IMessageLocalizer messageLocalizer,
        [FromServices] ILogger<WishlistsEndpointLogCategory> logger,
        CancellationToken cancellationToken)
    {
        const string operation = "Wishlists.Cards.Get";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            logger.LogOperationWarning(operation, "Missing user id", new { wishlistId, cardId });
            return Results.Unauthorized();
        }

        try
        {
            var wishlistCard = await wishlistService.GetWishlistCardByIdAsync(wishlistId, cardId, userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { wishlistId, cardId });
            return Results.Ok(wishlistCard);
        }
        catch (WishlistServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { wishlistId, cardId });
            return await MapWishlistServiceException(ex, context, messageLocalizer, userId);
        }
    }
}
