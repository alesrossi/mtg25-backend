using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using API.Dtos.Binders;
using API.Services;
using Core.Interfaces;
using Core.Models;
using Core.Specifications;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints;

public static class BindersEndpoint
{
    public static void MapBindersEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/binders").WithTags("Binders");

        group.MapGet("/", GetBindersAsync)
            .RequireAuthorization()
            .WithSummary("Get binders for user")
            .Produces<IEnumerable<BinderSummaryDto>>()
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/{id:int}", GetBinderByIdAsync)
            .RequireAuthorization()
            .WithSummary("Get binder by ID")
            .Produces<BinderDto>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/", CreateBinderAsync)
            .RequireAuthorization()
            .WithSummary("Create binder")
            .Produces<BinderDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapPut("/{id:int}", UpdateBinderAsync)
            .RequireAuthorization()
            .WithSummary("Update binder")
            .Produces<BinderDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:int}", DeleteBinderAsync)
            .RequireAuthorization()
            .WithSummary("Delete binder")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{binderId:int}/cards", GetBinderCardsAsync)
            .RequireAuthorization()
            .WithSummary("Get binder cards")
            .Produces<IEnumerable<BinderCardDto>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{binderId:int}/cards/{binderCardId:int}", GetBinderCardByIdAsync)
            .RequireAuthorization()
            .WithSummary("Get binder card")
            .Produces<BinderCardDto>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{binderId:int}/cards", CreateBinderCardAsync)
            .RequireAuthorization()
            .WithSummary("Add card to binder")
            .Produces<List<BinderCardDto>>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPut("/{binderId:int}/cards/{binderCardId:int}", UpdateBinderCardAsync)
            .RequireAuthorization()
            .WithSummary("Update binder card")
            .Produces<BinderCardDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/{binderId:int}/cards/{binderCardId:int}", DeleteBinderCardAsync)
            .RequireAuthorization()
            .WithSummary("Delete binder card")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> GetBindersAsync(
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

        var spec = new TradeBindersWithOwnerSpecification(userId, includeCards: true);
        var binders = await unitOfWork.Repository<TradeBinder>().ListAsync(spec) ?? Array.Empty<TradeBinder>();

        var dto = binders.Select(MapToSummaryDto).ToList();
        return Results.Ok(dto);
    }

    private static async Task<IResult> GetBinderByIdAsync(
        int id,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user)
    {
        var spec = new TradeBinderWithCardsSpecification(id);
        var binder = await unitOfWork.Repository<TradeBinder>().GetEntityWithSpec(spec);
        if (binder == null) return Results.NotFound();

        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var isOwner = !string.IsNullOrEmpty(userId) && binder.OwnerId == userId;

        if (!isOwner && !binder.IsPublic)
        {
            return Results.Unauthorized();
        }

        var cards = await unitOfWork.Repository<BinderCard>()
            .ListAsync(new BinderCardsWithBinderIdSpecification(binder.Id)) ?? Array.Empty<BinderCard>();

        return Results.Ok(MapToDto(binder, cards));
    }

    private static async Task<IResult> CreateBinderAsync(
        CreateBinderDto createDto,
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

        var binder = new TradeBinder
        {
            Name = createDto.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(createDto.Description) ? null : createDto.Description.Trim(),
            IsPublic = createDto.IsPublic,
            OwnerId = userId
        };

        unitOfWork.Repository<TradeBinder>().Add(binder);
        await unitOfWork.Complete();

        return Results.Ok(MapToDto(binder, Array.Empty<BinderCard>()));
    }

    private static async Task<IResult> UpdateBinderAsync(
        int id,
        UpdateBinderDto updateDto,
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

        var binder = await unitOfWork.Repository<TradeBinder>().GetByIdAsync(id);
        if (binder == null) return Results.NotFound();
        if (binder.OwnerId != userId) return Results.Unauthorized();

        binder.Name = updateDto.Name.Trim();
        binder.Description = string.IsNullOrWhiteSpace(updateDto.Description) ? null : updateDto.Description.Trim();
        binder.IsPublic = updateDto.IsPublic;

        unitOfWork.Repository<TradeBinder>().Update(binder);
        await unitOfWork.Complete();

        var updated = await unitOfWork.Repository<TradeBinder>().GetEntityWithSpec(new TradeBinderWithCardsSpecification(id)) ?? binder;
        var cards = await unitOfWork.Repository<BinderCard>()
            .ListAsync(new BinderCardsWithBinderIdSpecification(binder.Id)) ?? Array.Empty<BinderCard>();

        return Results.Ok(MapToDto(updated, cards));
    }

    private static async Task<IResult> DeleteBinderAsync(
        int id,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

        var binder = await unitOfWork.Repository<TradeBinder>().GetByIdAsync(id);
        if (binder == null) return Results.NotFound();
        if (binder.OwnerId != userId) return Results.Unauthorized();

        unitOfWork.Repository<TradeBinder>().Delete(binder);
        await unitOfWork.Complete();

        return Results.NoContent();
    }

    private static async Task<IResult> GetBinderCardsAsync(
        int binderId,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user)
    {
        var (result, binder) = await EnsureBinderAccessAsync(binderId, unitOfWork, user, allowPublic: true);
        if (result != null) return result;

        var cards = await unitOfWork.Repository<BinderCard>()
            .ListAsync(new BinderCardsWithBinderIdSpecification(binder!.Id)) ?? Array.Empty<BinderCard>();

        var dto = cards.Select(MapToDto).ToList();
        return Results.Ok(dto);
    }

    private static async Task<IResult> GetBinderCardByIdAsync(
        int binderId,
        int binderCardId,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user)
    {
        var (result, binder) = await EnsureBinderAccessAsync(binderId, unitOfWork, user, allowPublic: true);
        if (result != null) return result;

        var binderCard = await unitOfWork.Repository<BinderCard>().GetEntityWithSpec(new BinderCardWithBinderSpecification(binderCardId));
        if (binderCard == null || binderCard.TradeBinderId != binder!.Id)
        {
            return Results.NotFound();
        }

        return Results.Ok(MapToDto(binderCard));
    }

    private static async Task<IResult> CreateBinderCardAsync(
        int binderId,
        List<CreateBinderCardDto> createDtoList,
        IValidationService validationService,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user)
    {
        var (result, binder) = await EnsureBinderAccessAsync(binderId, unitOfWork, user, requireOwner: true);
        if (result != null) return result;

        var cardList = new List<BinderCardDto>();
        
        foreach (var createDto in createDtoList)
        {
            var (isValid, errors) = validationService.ValidateModel(createDto);
            if (!isValid)
            {
                return Results.BadRequest(new { errors });
            }

            var card = await unitOfWork.Repository<Card>().GetByIdAsync(createDto.CardId);
            if (card == null)
            {
                return Results.BadRequest(new { errors = new { CardId = new[] { "Card not found." } } });
            }

            var collection = await unitOfWork.Repository<Collection>().GetByIdAsync(card.CollectionId);
            if (collection == null || collection.OwnerId != binder!.OwnerId)
            {
                return Results.Unauthorized();
            }

            if (createDto.QuantityToTrade > card.Quantity)
            {
                return Results.BadRequest(new
                {
                    errors = new
                    {
                        QuantityToTrade = new[] { "Quantity to trade exceeds available card quantity." }
                    }
                });
            }

            var binderCard = new BinderCard
            {
                TradeBinderId = binder.Id,
                CardId = card.Id,
                Name = card.Name,
                QuantityToTrade = createDto.QuantityToTrade,
                Notes = string.IsNullOrWhiteSpace(createDto.Notes) ? null : createDto.Notes.Trim()
            };

            unitOfWork.Repository<BinderCard>().Add(binderCard);
            await unitOfWork.Complete();

            cardList.Add(MapToDto(await unitOfWork.Repository<BinderCard>().GetEntityWithSpec(new BinderCardWithBinderSpecification(binderCard.Id)) ?? binderCard));
        }

        return Results.Ok(cardList);
    }

    private static async Task<IResult> UpdateBinderCardAsync(
        int binderId,
        int binderCardId,
        UpdateBinderCardDto updateDto,
        IValidationService validationService,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user)
    {
        var (result, binder) = await EnsureBinderAccessAsync(binderId, unitOfWork, user, requireOwner: true);
        if (result != null) return result;

        var (isValid, errors) = validationService.ValidateModel(updateDto);
        if (!isValid)
        {
            return Results.BadRequest(new { errors });
        }

        var binderCard = await unitOfWork.Repository<BinderCard>().GetEntityWithSpec(new BinderCardWithBinderSpecification(binderCardId));
        if (binderCard == null || binderCard.TradeBinderId != binder!.Id)
        {
            return Results.NotFound();
        }

        if (binderCard.TradeBinder.OwnerId != binder.OwnerId)
        {
            return Results.Unauthorized();
        }

        var card = binderCard.Card ?? await unitOfWork.Repository<Card>().GetByIdAsync(binderCard.CardId);
        if (card == null)
        {
            return Results.BadRequest(new { errors = new { CardId = new[] { "Card not found." } } });
        }

        if (updateDto.QuantityToTrade > card.Quantity)
        {
            return Results.BadRequest(new
            {
                errors = new
                {
                    QuantityToTrade = new[] { "Quantity to trade exceeds available card quantity." }
                }
            });
        }

        binderCard.QuantityToTrade = updateDto.QuantityToTrade;
        binderCard.Notes = string.IsNullOrWhiteSpace(updateDto.Notes) ? null : updateDto.Notes.Trim();

        unitOfWork.Repository<BinderCard>().Update(binderCard);
        await unitOfWork.Complete();

        var updated = await unitOfWork.Repository<BinderCard>()
            .GetEntityWithSpec(new BinderCardWithBinderSpecification(binderCardId)) ?? binderCard;

        return Results.Ok(MapToDto(updated));
    }

    private static async Task<IResult> DeleteBinderCardAsync(
        int binderId,
        int binderCardId,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user)
    {
        var (result, binder) = await EnsureBinderAccessAsync(binderId, unitOfWork, user, requireOwner: true);
        if (result != null) return result;

        var binderCard = await unitOfWork.Repository<BinderCard>().GetEntityWithSpec(new BinderCardWithBinderSpecification(binderCardId));
        if (binderCard == null || binderCard.TradeBinderId != binder!.Id)
        {
            return Results.NotFound();
        }

        unitOfWork.Repository<BinderCard>().Delete(binderCard);
        await unitOfWork.Complete();

        return Results.NoContent();
    }

    private static async Task<(IResult? Result, TradeBinder? Binder)> EnsureBinderAccessAsync(
        int binderId,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user,
        bool allowPublic = false,
        bool requireOwner = false)
    {
        var binder = await unitOfWork.Repository<TradeBinder>().GetByIdAsync(binderId);
        if (binder == null) return (Results.NotFound(), null);

        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var isOwner = !string.IsNullOrEmpty(userId) && binder.OwnerId == userId;

        if (requireOwner)
        {
            if (!isOwner) return (Results.Unauthorized(), null);
            return (null, binder);
        }

        if (isOwner) return (null, binder);
        if (allowPublic && binder.IsPublic) return (null, binder);

        return (Results.Unauthorized(), null);
    }

    private static BinderSummaryDto MapToSummaryDto(TradeBinder binder)
    {
        return new BinderSummaryDto
        {
            Id = binder.Id,
            Name = binder.Name,
            Description = binder.Description,
            IsPublic = binder.IsPublic,
            CardsCount = binder.BinderCards?.Count ?? 0
        };
    }

    private static BinderDto MapToDto(TradeBinder binder, IEnumerable<BinderCard>? binderCards = null)
    {
        var cards = binderCards?.ToList() ?? binder.BinderCards?.ToList() ?? new List<BinderCard>();
        var cardDtos = cards.Select(MapToDto).ToList();

        return new BinderDto
        {
            Id = binder.Id,
            Name = binder.Name,
            Description = binder.Description,
            IsPublic = binder.IsPublic,
            OwnerId = binder.OwnerId,
            CardsCount = cardDtos.Count,
            Cards = cardDtos
        };
    }

    private static BinderCardDto MapToDto(BinderCard card)
    {
        return new BinderCardDto
        {
            Id = card.Id,
            TradeBinderId = card.TradeBinderId,
            CardId = card.CardId,
            Card = card.Card!,
            Name = card.Name,
            QuantityToTrade = card.QuantityToTrade,
            Notes = card.Notes,
            ImageUrl = card.Card?.ImageUrl,
            SetCode = card.Card?.SetCode,
            SetName = card.Card?.SetName,
            CollectorNumber = card.Card?.CollectorNumber,
            Rarity = card.Card?.Rarity
        };
    }
}
