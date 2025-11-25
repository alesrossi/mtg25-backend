using System.Linq;
using System.Security.Claims;
using API.Dtos.Wishlists;
using Core.Interfaces;
using Core.Models;
using Core.Specifications;

namespace API.Endpoints;

public static partial class WishlistsEndpoint
{
    private static async Task<IResult> GetWishlistsAsync(
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

        var spec = new WishlistsWithOwnerSpecification(userId, includeCards: true);
        var wishlists = await unitOfWork.Repository<Wishlist>().ListAsync(spec) ?? Array.Empty<Wishlist>();

        var dto = wishlists.Select(MapToSummaryDto).ToList();
        return Results.Ok(dto);
    }

    private static async Task<IResult> GetWishlistByIdAsync(
        int id,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

        var spec = new WishlistWithCardsSpecification(id, userId);
        var wishlist = await unitOfWork.Repository<Wishlist>().GetEntityWithSpec(spec);
        if (wishlist == null) return Results.NotFound();

        return Results.Ok(MapToDto(wishlist));
    }

    private static async Task<IResult> GetWishlistCardsAsync(
        int wishlistId,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user)
    {
        var ownershipResult = await EnsureWishlistOwnershipAsync(wishlistId, unitOfWork, user);
        if (ownershipResult.Result != null) return ownershipResult.Result;

        var spec = new WishlistCardsWithWishlistIdSpecification(wishlistId);
        var cards = await unitOfWork.Repository<WishlistCard>().ListAsync(spec) ?? Array.Empty<WishlistCard>();

        return Results.Ok(cards.Select(MapToDto).ToList());
    }

    private static async Task<IResult> GetWishlistCardByIdAsync(
        int wishlistId,
        int cardId,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user)
    {
        var ownershipResult = await EnsureWishlistOwnershipAsync(wishlistId, unitOfWork, user);
        if (ownershipResult.Result != null) return ownershipResult.Result;

        var wishlistCard = await unitOfWork.Repository<WishlistCard>().GetByIdAsync(cardId);
        return wishlistCard == null ? Results.NotFound() : Results.Ok(MapToDto(wishlistCard));
    }
}
