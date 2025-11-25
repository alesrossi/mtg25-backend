using System.Collections.Generic;
using System.Security.Claims;
using API.Dtos.Wishlists;
using API.Services;
using Core.Interfaces;
using Core.Models;
using Core.Specifications;

namespace API.Endpoints;

public static partial class WishlistsEndpoint
{
    private static async Task<IResult> CreateWishlistAsync(
        CreateWishlistDto createDto,
        IValidationService validationService,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

        var (isValid, errors) = validationService.ValidateModel(createDto);
        if (!isValid)
        {
            return Results.BadRequest(new { errors });
        }

        var wishlist = new Wishlist
        {
            Name = createDto.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(createDto.Description) ? null : createDto.Description.Trim(),
            IsPublic = createDto.IsPublic,
            OwnerId = userId
        };

        unitOfWork.Repository<Wishlist>().Add(wishlist);
        await unitOfWork.Complete();

        return Results.Ok(MapToDto(wishlist));
    }

    private static async Task<IResult> UpdateWishlistAsync(
        int id,
        UpdateWishlistDto updateDto,
        IValidationService validationService,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

        var (isValid, errors) = validationService.ValidateModel(updateDto);
        if (!isValid)
        {
            return Results.BadRequest(new { errors });
        }

        var wishlist = await unitOfWork.Repository<Wishlist>().GetByIdAsync(id);
        if (wishlist == null) return Results.NotFound();
        if (wishlist.OwnerId != userId) return Results.Unauthorized();

        wishlist.Name = updateDto.Name.Trim();
        wishlist.Description = string.IsNullOrWhiteSpace(updateDto.Description) ? null : updateDto.Description.Trim();
        wishlist.IsPublic = updateDto.IsPublic;

        unitOfWork.Repository<Wishlist>().Update(wishlist);
        await unitOfWork.Complete();

        var spec = new WishlistWithCardsSpecification(id, userId);
        var updated = await unitOfWork.Repository<Wishlist>().GetEntityWithSpec(spec) ?? wishlist;

        return Results.Ok(MapToDto(updated));
    }

    private static async Task<IResult> DeleteWishlistAsync(
        int id,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

        var wishlist = await unitOfWork.Repository<Wishlist>().GetByIdAsync(id);
        if (wishlist == null) return Results.NotFound();
        if (wishlist.OwnerId != userId) return Results.Unauthorized();

        unitOfWork.Repository<Wishlist>().Delete(wishlist);
        await unitOfWork.Complete();

        return Results.NoContent();
    }

    private static async Task<IResult> CreateWishlistCardAsync(
        int wishlistId,
        List<CreateWishlistCardDto> newWishlistCardList,
        IValidationService validationService,
        IUnitOfWork unitOfWork,
        CardDataService cds,
        ClaimsPrincipal user)
    {
        var ownershipResult = await EnsureWishlistOwnershipAsync(wishlistId, unitOfWork, user);
        if (ownershipResult.Result != null) return ownershipResult.Result;

        var (isValid, errors) = validationService.ValidateModel(newWishlistCardList);
        if (!isValid)
        {
            return Results.BadRequest(new { errors });
        }

        var cardList = newWishlistCardList
            .Where(x => cds.CardDataById.ContainsKey(x.OracleId))
            .Select(x =>
            {
                var card = cds.CardDataById[x.OracleId];
                return new WishlistCard
                {
                    WishlistId = wishlistId,
                    DesiredQuantity = x.DesiredQuantity,
                    IsFoil = x.IsFoil,
                    Language = x.Language,
                    Name = card.Name,
                    OracleId = x.OracleId,
                    Notes = x.Notes,
                    OriginalDeckId = x.OriginalDeckId
                };
            }).ToList();

        unitOfWork.Repository<WishlistCard>().Add(cardList);
        await unitOfWork.Complete();

        return Results.Ok(cardList);
    }

    private static async Task<IResult> UpdateWishlistCardAsync(
        int wishlistId,
        int cardId,
        UpdateWishlistCardDto updateDto,
        IValidationService validationService,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user)
    {
        var ownershipResult = await EnsureWishlistOwnershipAsync(wishlistId, unitOfWork, user);
        if (ownershipResult.Result != null) return ownershipResult.Result;

        var (isValid, errors) = validationService.ValidateModel(updateDto);
        if (!isValid)
        {
            return Results.BadRequest(new { errors });
        }

        var wishlistCard = await unitOfWork.Repository<WishlistCard>().GetByIdAsync(cardId);
        if (wishlistCard == null || wishlistCard.WishlistId != wishlistId) return Results.NotFound();

        wishlistCard.Name = updateDto.Name.Trim();
        wishlistCard.DesiredQuantity = updateDto.DesiredQuantity;
        wishlistCard.IsFoil = updateDto.IsFoil;
        wishlistCard.Language = string.IsNullOrWhiteSpace(updateDto.Language) ? null : updateDto.Language.Trim();
        wishlistCard.Notes = updateDto.Notes?.Trim() ?? string.Empty;

        unitOfWork.Repository<WishlistCard>().Update(wishlistCard);
        await unitOfWork.Complete();

        return Results.Ok(MapToDto(wishlistCard));
    }

    private static async Task<IResult> DeleteWishlistCardAsync(
        int wishlistId,
        int cardId,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user)
    {
        var ownershipResult = await EnsureWishlistOwnershipAsync(wishlistId, unitOfWork, user);
        if (ownershipResult.Result != null) return ownershipResult.Result;

        var wishlistCard = await unitOfWork.Repository<WishlistCard>().GetByIdAsync(cardId);
        if (wishlistCard == null || wishlistCard.WishlistId != wishlistId) return Results.NotFound();

        unitOfWork.Repository<WishlistCard>().Delete(wishlistCard);
        await unitOfWork.Complete();

        return Results.NoContent();
    }
}
