using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using API.Dtos.Binders;
using API.Services;
using Core.Interfaces;
using Core.Models;
using Core.Specifications;
using Microsoft.AspNetCore.Routing;

namespace API.Endpoints;

public static partial class BindersEndpoint
{
    private static void MapBinderCommands(RouteGroupBuilder group)
    {
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
}
