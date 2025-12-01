using System.Security.Claims;
using API.Logging;
using Core.Interfaces;
using Core.Models;
using Core.Specifications;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints.Wishlists;

public static partial class WishlistsEndpoint
{
    private static async Task<IResult> GetWishlistsAsync(
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user,
        [FromServices] ILogger<WishlistsEndpointLogCategory> logger)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            logger.LogOperationWarning("Wishlists.QueryAll", "Missing user id");
            return Results.Unauthorized();
        }

        var spec = new WishlistsWithOwnerSpecification(userId, includeCards: true);
        var wishlists = await unitOfWork.Repository<Wishlist>().ListAsync(spec, tracking: false) ?? Array.Empty<Wishlist>();

        var dto = wishlists.Select(MapToSummaryDto).ToList();
        logger.LogOperationSuccess("Wishlists.QueryAll", new { userId, Count = dto.Count });
        return Results.Ok(dto);
    }

    private static async Task<IResult> GetWishlistByIdAsync(
        int id,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user,
        [FromServices] ILogger<WishlistsEndpointLogCategory> logger)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            logger.LogOperationWarning("Wishlists.Get", "Missing user id", new { id });
            return Results.Unauthorized();
        }

        var spec = new WishlistWithCardsSpecification(id, userId);
        var wishlist = await unitOfWork.Repository<Wishlist>().GetEntityWithSpec(spec, tracking: false);
        if (wishlist == null)
        {
            logger.LogOperationWarning("Wishlists.Get", "Wishlist not found", new { id });
            return Results.NotFound();
        }

        logger.LogOperationSuccess("Wishlists.Get", new { id });
        return Results.Ok(MapToDto(wishlist));
    }

    private static async Task<IResult> GetWishlistCardsAsync(
        int wishlistId,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user,
        [FromServices] ILogger<WishlistsEndpointLogCategory> logger)
    {
        var ownershipResult = await EnsureWishlistOwnershipAsync(wishlistId, unitOfWork, user, tracking: false);
        if (ownershipResult.Result != null)
        {
            logger.LogOperationWarning("Wishlists.Cards.Query", "Access denied", new { wishlistId });
            return ownershipResult.Result;
        }

        var spec = new WishlistCardsWithWishlistIdSpecification(wishlistId);
        var cards = await unitOfWork.Repository<WishlistCard>().ListAsync(spec, tracking: false) ?? Array.Empty<WishlistCard>();

        var dto = cards.Select(MapToDto).ToList();
        logger.LogOperationSuccess("Wishlists.Cards.Query", new { wishlistId, Count = dto.Count });
        return Results.Ok(dto);
    }

    private static async Task<IResult> GetWishlistCardByIdAsync(
        int wishlistId,
        int cardId,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user,
        [FromServices] ILogger<WishlistsEndpointLogCategory> logger)
    {
        var ownershipResult = await EnsureWishlistOwnershipAsync(wishlistId, unitOfWork, user, tracking: false);
        if (ownershipResult.Result != null)
        {
            logger.LogOperationWarning("Wishlists.Cards.Get", "Access denied", new { wishlistId, cardId });
            return ownershipResult.Result;
        }

        var wishlistCard = await unitOfWork.Repository<WishlistCard>().GetByIdAsync(cardId, tracking: false);
        if (wishlistCard == null)
        {
            logger.LogOperationWarning("Wishlists.Cards.Get", "Wishlist card not found", new { wishlistId, cardId });
            return Results.NotFound();
        }

        logger.LogOperationSuccess("Wishlists.Cards.Get", new { wishlistId, cardId });
        return Results.Ok(MapToDto(wishlistCard));
    }
}
