using System.Security.Claims;
using API.Dtos.Wishlists;
using Core.Interfaces;
using Core.Models;

namespace API.Endpoints.Wishlists;

public static partial class WishlistsEndpoint
{
    private static async Task<(IResult? Result, Wishlist? Wishlist)> EnsureWishlistOwnershipAsync(
        int wishlistId,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user,
        bool tracking = true)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return (Results.Unauthorized(), null);

        var wishlist = await unitOfWork.Repository<Wishlist>().GetByIdAsync(wishlistId, tracking);
        if (wishlist == null) return (Results.NotFound(), null);
        if (wishlist.OwnerId != userId) return (Results.Unauthorized(), null);

        return (null, wishlist);
    }

    private static WishlistSummaryDto MapToSummaryDto(Wishlist wishlist)
    {
        var cards = wishlist.WishlistCards ?? new List<WishlistCard>();

        return new WishlistSummaryDto
        {
            Id = wishlist.Id,
            Name = wishlist.Name,
            Description = wishlist.Description,
            IsPublic = wishlist.IsPublic,
            CardsCount = cards.Sum(c => c.DesiredQuantity),
            IndividualCardsCount = cards.Count
        };
    }

    private static WishlistDto MapToDto(Wishlist wishlist)
    {
        var cards = wishlist.WishlistCards?.Select(MapToDto).ToList() ?? new List<WishlistCardDto>();

        return new WishlistDto
        {
            Id = wishlist.Id,
            Name = wishlist.Name,
            Description = wishlist.Description,
            IsPublic = wishlist.IsPublic,
            OwnerId = wishlist.OwnerId,
            CardsCount = cards.Sum(c => c.DesiredQuantity),
            IndividualCardsCount = cards.Count,
            Cards = cards
        };
    }

    private static WishlistCardDto MapToDto(WishlistCard card)
    {
        return new WishlistCardDto
        {
            Id = card.Id,
            WishlistId = card.WishlistId,
            OracleId = card.OracleId,
            Name = card.Name,
            ImageUrl = card.ImageUrl,
            BackImageUrl = card.BackImageUrl,
            DesiredQuantity = card.DesiredQuantity,
            IsFoil = card.IsFoil ?? false,
            Language = card.Language,
            Notes = card.Notes
        };
    }
}
