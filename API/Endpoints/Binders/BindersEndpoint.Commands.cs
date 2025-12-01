using System.Security.Claims;
using API.Dtos.Binders;
using API.Logging;
using API.Services;
using Core.Interfaces;
using Core.Models;
using Core.Specifications;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints.Binders;

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
        ClaimsPrincipal user,
        [FromServices] ILogger<BindersEndpointLogCategory> logger)
    {
        const string operation = "Binders.Create";
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            logger.LogOperationWarning(operation, "Missing user id");
            return Results.Unauthorized();
        }

        logger.LogOperationStart(operation, new { createDto.Name });

        var (isValid, errors) = validationService.ValidateModel(createDto);
        if (!isValid)
        {
            logger.LogOperationWarning(operation, "Validation failed", new { errors });
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

        logger.LogOperationSuccess(operation, new { binder.Id });
        return Results.Ok(MapToDto(binder, Array.Empty<BinderCard>()));
    }

    private static async Task<IResult> UpdateBinderAsync(
        int id,
        UpdateBinderDto updateDto,
        IValidationService validationService,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user,
        [FromServices] ILogger<BindersEndpointLogCategory> logger)
    {
        const string operation = "Binders.Update";
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            logger.LogOperationWarning(operation, "Missing user id", new { id });
            return Results.Unauthorized();
        }

        var (isValid, errors) = validationService.ValidateModel(updateDto);
        if (!isValid)
        {
            logger.LogOperationWarning(operation, "Validation failed", new { id, errors });
            return Results.BadRequest(new { errors });
        }

        var binder = await unitOfWork.Repository<TradeBinder>().GetByIdAsync(id);
        if (binder == null)
        {
            logger.LogOperationWarning(operation, "Binder not found", new { id });
            return Results.NotFound();
        }
        if (binder.OwnerId != userId)
        {
            logger.LogOperationWarning(operation, "Unauthorized", new { id, userId });
            return Results.Unauthorized();
        }

        binder.Name = updateDto.Name.Trim();
        binder.Description = string.IsNullOrWhiteSpace(updateDto.Description) ? null : updateDto.Description.Trim();
        binder.IsPublic = updateDto.IsPublic;

        unitOfWork.Repository<TradeBinder>().Update(binder);
        await unitOfWork.Complete();

        var updated = await unitOfWork.Repository<TradeBinder>().GetEntityWithSpec(new TradeBinderWithCardsSpecification(id)) ?? binder;
        var cards = await unitOfWork.Repository<BinderCard>()
            .ListAsync(new BinderCardsWithBinderIdSpecification(binder.Id)) ?? Array.Empty<BinderCard>();

        logger.LogOperationSuccess(operation, new { id });
        return Results.Ok(MapToDto(updated, cards));
    }

    private static async Task<IResult> DeleteBinderAsync(
        int id,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user,
        [FromServices] ILogger<BindersEndpointLogCategory> logger)
    {
        const string operation = "Binders.Delete";
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            logger.LogOperationWarning(operation, "Missing user id", new { id });
            return Results.Unauthorized();
        }

        var binder = await unitOfWork.Repository<TradeBinder>().GetByIdAsync(id);
        if (binder == null)
        {
            logger.LogOperationWarning(operation, "Binder not found", new { id });
            return Results.NotFound();
        }
        if (binder.OwnerId != userId)
        {
            logger.LogOperationWarning(operation, "Unauthorized", new { id, userId });
            return Results.Unauthorized();
        }

        unitOfWork.Repository<TradeBinder>().Delete(binder);
        await unitOfWork.Complete();

        logger.LogOperationSuccess(operation, new { id });
        return Results.NoContent();
    }

    private static async Task<IResult> CreateBinderCardAsync(
        int binderId,
        List<CreateBinderCardDto> createDtoList,
        IValidationService validationService,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user,
        [FromServices] ILogger<BindersEndpointLogCategory> logger)
    {
        const string operation = "Binders.Cards.Create";
        var (result, binder) = await EnsureBinderAccessAsync(binderId, unitOfWork, user, requireOwner: true);
        if (result != null) return result;

        var cardList = new List<BinderCardDto>();

        foreach (var createDto in createDtoList)
        {
            var (isValid, errors) = validationService.ValidateModel(createDto);
            if (!isValid)
            {
                logger.LogOperationWarning(operation, "Validation failed", new { binderId, errors });
                return Results.BadRequest(new { errors });
            }

            var card = await unitOfWork.Repository<Card>().GetByIdAsync(createDto.CardId);
            if (card == null)
            {
                logger.LogOperationWarning(operation, "Card not found", new { createDto.CardId });
                return Results.BadRequest(new { errors = new { CardId = new[] { "Card not found." } } });
            }

            var collection = await unitOfWork.Repository<Collection>().GetByIdAsync(card.CollectionId);
            if (collection == null || collection.OwnerId != binder!.OwnerId)
            {
                logger.LogOperationWarning(operation, "Collection access denied", new { binderId, card.CollectionId });
                return Results.Unauthorized();
            }

            if (createDto.QuantityToTrade > card.Quantity)
            {
                logger.LogOperationWarning(operation, "Quantity exceeds available", new { binderId, createDto.CardId });
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

        logger.LogOperationSuccess(operation, new { binderId, Added = cardList.Count });
        return Results.Ok(cardList);
    }

    private static async Task<IResult> UpdateBinderCardAsync(
        int binderId,
        int binderCardId,
        UpdateBinderCardDto updateDto,
        IValidationService validationService,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user,
        [FromServices] ILogger<BindersEndpointLogCategory> logger)
    {
        const string operation = "Binders.Cards.Update";
        var (result, binder) = await EnsureBinderAccessAsync(binderId, unitOfWork, user, requireOwner: true);
        if (result != null)
        {
            logger.LogOperationWarning(operation, "Access denied", new { binderId, binderCardId });
            return result;
        }

        var (isValid, errors) = validationService.ValidateModel(updateDto);
        if (!isValid)
        {
            logger.LogOperationWarning(operation, "Validation failed", new { binderId, binderCardId, errors });
            return Results.BadRequest(new { errors });
        }

        var binderCard = await unitOfWork.Repository<BinderCard>().GetEntityWithSpec(new BinderCardWithBinderSpecification(binderCardId));
        if (binderCard == null || binderCard.TradeBinderId != binder!.Id)
        {
            logger.LogOperationWarning(operation, "Binder card not found", new { binderId, binderCardId });
            return Results.NotFound();
        }

        if (binderCard.TradeBinder.OwnerId != binder.OwnerId)
        {
            logger.LogOperationWarning(operation, "Unauthorized", new { binderId, binderCardId });
            return Results.Unauthorized();
        }

        var card = binderCard.Card ?? await unitOfWork.Repository<Card>().GetByIdAsync(binderCard.CardId);
        if (card == null)
        {
            logger.LogOperationWarning(operation, "Card not found", new { binderCardId });
            return Results.BadRequest(new { errors = new { CardId = new[] { "Card not found." } } });
        }

        if (updateDto.QuantityToTrade > card.Quantity)
        {
            logger.LogOperationWarning(operation, "Quantity exceeds available", new { binderCardId });
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

        logger.LogOperationSuccess(operation, new { binderId, binderCardId });
        return Results.Ok(MapToDto(updated));
    }

    private static async Task<IResult> DeleteBinderCardAsync(
        int binderId,
        int binderCardId,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user,
        [FromServices] ILogger<BindersEndpointLogCategory> logger)
    {
        const string operation = "Binders.Cards.Delete";
        var (result, binder) = await EnsureBinderAccessAsync(binderId, unitOfWork, user, requireOwner: true);
        if (result != null)
        {
            logger.LogOperationWarning(operation, "Access denied", new { binderId, binderCardId });
            return result;
        }

        var binderCard = await unitOfWork.Repository<BinderCard>().GetEntityWithSpec(new BinderCardWithBinderSpecification(binderCardId));
        if (binderCard == null || binderCard.TradeBinderId != binder!.Id)
        {
            logger.LogOperationWarning(operation, "Binder card not found", new { binderId, binderCardId });
            return Results.NotFound();
        }

        unitOfWork.Repository<BinderCard>().Delete(binderCard);
        await unitOfWork.Complete();

        logger.LogOperationSuccess(operation, new { binderId, binderCardId });
        return Results.NoContent();
    }
}
