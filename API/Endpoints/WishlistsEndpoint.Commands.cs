using System.Collections.Generic;
using System.Security.Claims;
using API.Dtos.Wishlists;
using API.Logging;
using API.Services;
using Core.Interfaces;
using Core.Models;
using Core.Specifications;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace API.Endpoints;

public static partial class WishlistsEndpoint
{
    private static async Task<IResult> CreateWishlistAsync(
        CreateWishlistDto createDto,
        IValidationService validationService,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user,
        [FromServices] ILogger<WishlistsEndpointLogCategory> logger)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            logger.LogOperationWarning("Wishlists.Create", "Missing user id");
            return Results.Unauthorized();
        }

        var (isValid, errors) = validationService.ValidateModel(createDto);
        if (!isValid)
        {
            logger.LogOperationWarning("Wishlists.Create", "Validation failed", new { errors });
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

        logger.LogOperationSuccess("Wishlists.Create", new { wishlist.Id });
        return Results.Ok(MapToDto(wishlist));
    }

    private static async Task<IResult> UpdateWishlistAsync(
        int id,
        UpdateWishlistDto updateDto,
        IValidationService validationService,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user,
        [FromServices] ILogger<WishlistsEndpointLogCategory> logger)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            logger.LogOperationWarning("Wishlists.Update", "Missing user id", new { id });
            return Results.Unauthorized();
        }

        var (isValid, errors) = validationService.ValidateModel(updateDto);
        if (!isValid)
        {
            logger.LogOperationWarning("Wishlists.Update", "Validation failed", new { id, errors });
            return Results.BadRequest(new { errors });
        }

        var wishlist = await unitOfWork.Repository<Wishlist>().GetByIdAsync(id);
        if (wishlist == null)
        {
            logger.LogOperationWarning("Wishlists.Update", "Wishlist not found", new { id });
            return Results.NotFound();
        }
        if (wishlist.OwnerId != userId)
        {
            logger.LogOperationWarning("Wishlists.Update", "Unauthorized", new { id, userId });
            return Results.Unauthorized();
        }

        wishlist.Name = updateDto.Name.Trim();
        wishlist.Description = string.IsNullOrWhiteSpace(updateDto.Description) ? null : updateDto.Description.Trim();
        wishlist.IsPublic = updateDto.IsPublic;

        unitOfWork.Repository<Wishlist>().Update(wishlist);
        await unitOfWork.Complete();

        var spec = new WishlistWithCardsSpecification(id, userId);
        var updated = await unitOfWork.Repository<Wishlist>().GetEntityWithSpec(spec) ?? wishlist;

        logger.LogOperationSuccess("Wishlists.Update", new { id });
        return Results.Ok(MapToDto(updated));
    }

    private static async Task<IResult> DeleteWishlistAsync(
        int id,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user,
        [FromServices] ILogger<WishlistsEndpointLogCategory> logger)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            logger.LogOperationWarning("Wishlists.Delete", "Missing user id", new { id });
            return Results.Unauthorized();
        }

        var wishlist = await unitOfWork.Repository<Wishlist>().GetByIdAsync(id);
        if (wishlist == null)
        {
            logger.LogOperationWarning("Wishlists.Delete", "Wishlist not found", new { id });
            return Results.NotFound();
        }
        if (wishlist.OwnerId != userId)
        {
            logger.LogOperationWarning("Wishlists.Delete", "Unauthorized", new { id, userId });
            return Results.Unauthorized();
        }

        unitOfWork.Repository<Wishlist>().Delete(wishlist);
        await unitOfWork.Complete();

        logger.LogOperationSuccess("Wishlists.Delete", new { id });
        return Results.NoContent();
    }

    private static async Task<IResult> CreateWishlistCardAsync(
        int wishlistId,
        List<CreateWishlistCardDto> newWishlistCardList,
        IValidationService validationService,
        IUnitOfWork unitOfWork,
        CardDataService cds,
        ClaimsPrincipal user,
        [FromServices] ILogger<WishlistsEndpointLogCategory> logger)
    {
        var ownershipResult = await EnsureWishlistOwnershipAsync(wishlistId, unitOfWork, user);
        if (ownershipResult.Result != null)
        {
            logger.LogOperationWarning("Wishlists.Cards.Create", "Access denied", new { wishlistId });
            return ownershipResult.Result;
        }

        var (isValid, errors) = validationService.ValidateModel(newWishlistCardList);
        if (!isValid)
        {
            logger.LogOperationWarning("Wishlists.Cards.Create", "Validation failed", new { wishlistId, errors });
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

        logger.LogOperationSuccess("Wishlists.Cards.Create", new { wishlistId, Added = cardList.Count });
        return Results.Ok(cardList);
    }

    private static async Task<IResult> UpdateWishlistCardAsync(
        int wishlistId,
        int cardId,
        UpdateWishlistCardDto updateDto,
        IValidationService validationService,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user,
        [FromServices] ILogger<WishlistsEndpointLogCategory> logger)
    {
        var ownershipResult = await EnsureWishlistOwnershipAsync(wishlistId, unitOfWork, user);
        if (ownershipResult.Result != null)
        {
            logger.LogOperationWarning("Wishlists.Cards.Update", "Access denied", new { wishlistId, cardId });
            return ownershipResult.Result;
        }

        var (isValid, errors) = validationService.ValidateModel(updateDto);
        if (!isValid)
        {
            logger.LogOperationWarning("Wishlists.Cards.Update", "Validation failed", new { wishlistId, cardId, errors });
            return Results.BadRequest(new { errors });
        }

        var wishlistCard = await unitOfWork.Repository<WishlistCard>().GetByIdAsync(cardId);
        if (wishlistCard == null || wishlistCard.WishlistId != wishlistId)
        {
            logger.LogOperationWarning("Wishlists.Cards.Update", "Wishlist card not found", new { wishlistId, cardId });
            return Results.NotFound();
        }

        wishlistCard.Name = updateDto.Name.Trim();
        wishlistCard.DesiredQuantity = updateDto.DesiredQuantity;
        wishlistCard.IsFoil = updateDto.IsFoil;
        wishlistCard.Language = string.IsNullOrWhiteSpace(updateDto.Language) ? null : updateDto.Language.Trim();
        wishlistCard.Notes = updateDto.Notes?.Trim() ?? string.Empty;

        unitOfWork.Repository<WishlistCard>().Update(wishlistCard);
        await unitOfWork.Complete();

        logger.LogOperationSuccess("Wishlists.Cards.Update", new { wishlistId, cardId });
        return Results.Ok(MapToDto(wishlistCard));
    }

    private static async Task<IResult> DeleteWishlistCardAsync(
        int wishlistId,
        int cardId,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user,
        [FromServices] ILogger<WishlistsEndpointLogCategory> logger)
    {
        var ownershipResult = await EnsureWishlistOwnershipAsync(wishlistId, unitOfWork, user);
        if (ownershipResult.Result != null)
        {
            logger.LogOperationWarning("Wishlists.Cards.Delete", "Access denied", new { wishlistId, cardId });
            return ownershipResult.Result;
        }

        var wishlistCard = await unitOfWork.Repository<WishlistCard>().GetByIdAsync(cardId);
        if (wishlistCard == null || wishlistCard.WishlistId != wishlistId)
        {
            logger.LogOperationWarning("Wishlists.Cards.Delete", "Wishlist card not found", new { wishlistId, cardId });
            return Results.NotFound();
        }

        unitOfWork.Repository<WishlistCard>().Delete(wishlistCard);
        await unitOfWork.Complete();

        logger.LogOperationSuccess("Wishlists.Cards.Delete", new { wishlistId, cardId });
        return Results.NoContent();
    }
}
