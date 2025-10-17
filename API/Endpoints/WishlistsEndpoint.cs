using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using API.Dtos.Wishlists;
using API.Services;
using Core.Interfaces;
using Core.Models;
using Core.Specifications;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints;

public static class WishlistsEndpoint
{
    public static void MapWishlistsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/wishlists").WithTags("Wishlists");

        group.MapGet("/", GetWishlistsAsync)
            .RequireAuthorization()
            .WithSummary("Get wishlists for user")
            .Produces<IEnumerable<WishlistSummaryDto>>()
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/{id:int}", GetWishlistByIdAsync)
            .RequireAuthorization()
            .WithSummary("Get wishlist by ID")
            .Produces<WishlistDto>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/", CreateWishlistAsync)
            .RequireAuthorization()
            .WithSummary("Create wishlist")
            .Produces<WishlistDto>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapPut("/{id:int}", UpdateWishlistAsync)
            .RequireAuthorization()
            .WithSummary("Update wishlist")
            .Produces<WishlistDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:int}", DeleteWishlistAsync)
            .RequireAuthorization()
            .WithSummary("Delete wishlist")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{wishlistId:int}/cards", GetWishlistCardsAsync)
            .RequireAuthorization()
            .WithSummary("Get wishlist cards")
            .Produces<IEnumerable<WishlistCardDto>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{wishlistId:int}/cards/{cardId:int}", GetWishlistCardByIdAsync)
            .RequireAuthorization()
            .WithSummary("Get wishlist card")
            .Produces<WishlistCardDto>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{wishlistId:int}/cards", CreateWishlistCardAsync)
            .RequireAuthorization()
            .WithSummary("Add card to wishlist")
            .Produces<List<WishlistCard>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPut("/{wishlistId:int}/cards/{cardId:int}", UpdateWishlistCardAsync)
            .RequireAuthorization()
            .WithSummary("Update wishlist card")
            .Produces<WishlistCardDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/{wishlistId:int}/cards/{cardId:int}", DeleteWishlistCardAsync)
            .RequireAuthorization()
            .WithSummary("Delete wishlist card")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
    }

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

        return Results.Created($"/api/wishlists/{wishlist.Id}", MapToDto(wishlist));
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
        if (wishlistCard == null || wishlistCard.Id != wishlistId)
        {
            return Results.NotFound();
        }

        return Results.Ok(MapToDto(wishlistCard));
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
                    Notes = x.Notes

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

    private static async Task<(IResult? Result, Wishlist? Wishlist)> EnsureWishlistOwnershipAsync(
        int wishlistId,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return (Results.Unauthorized(), null);

        var wishlist = await unitOfWork.Repository<Wishlist>().GetByIdAsync(wishlistId);
        if (wishlist == null) return (Results.NotFound(), null);
        if (wishlist.OwnerId != userId) return (Results.Unauthorized(), null);

        return (null, wishlist);
    }

    private static WishlistSummaryDto MapToSummaryDto(Wishlist wishlist)
    {
        return new WishlistSummaryDto
        {
            Id = wishlist.Id,
            Name = wishlist.Name,
            Description = wishlist.Description,
            IsPublic = wishlist.IsPublic,
            CardsCount = wishlist.WishlistCards?.Count ?? 0
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
            CardsCount = cards.Count,
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
            DesiredQuantity = card.DesiredQuantity,
            IsFoil = card.IsFoil ?? false,
            Language = card.Language,
            Notes = card.Notes
        };
    }
}
