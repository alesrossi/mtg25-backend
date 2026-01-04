using System.Linq;
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
    private static async Task<IResult> GetBindersAsync(
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user,
        [FromServices] ILogger<BindersEndpointLogCategory> logger)
    {
        const string operation = "Binders.List";
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            logger.LogOperationWarning(operation, "Missing user id");
            return Results.Unauthorized();
        }

        var spec = new TradeBindersWithOwnerSpecification(userId, includeCards: true);
        var binders = await unitOfWork.Repository<TradeBinder>().ListAsync(spec, tracking: false) ?? Array.Empty<TradeBinder>();

        if (binders.Count > 0)
        {
            var binderIds = binders.Select(b => b.Id).ToArray();
            var binderCards = await unitOfWork.Repository<BinderCard>()
                .ListAsync(new BinderCardsByBinderIdsSpecification(binderIds), tracking: false) ?? Array.Empty<BinderCard>();
            var cardsGrouped = binderCards
                .GroupBy(card => card.TradeBinderId)
                .ToDictionary(group => group.Key, group => group.ToList());

            foreach (var binder in binders)
            {
                if (cardsGrouped.TryGetValue(binder.Id, out var cards))
                {
                    binder.BinderCards = cards;
                }
            }
        }

        var dto = binders.Select(MapToSummaryDto).ToList();
        logger.LogOperationSuccess(operation, new { userId, Count = dto.Count });
        return Results.Ok(dto);
    }

    private static async Task<IResult> GetBinderByIdAsync(
        int id,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user,
        [FromServices] IUserSettingsService userSettingsService,
        [FromServices] CardDataService cardDataService,
        [FromServices] ILogger<BindersEndpointLogCategory> logger)
    {
        const string operation = "Binders.Get";
        var spec = new TradeBinderWithCardsSpecification(id);
        var binder = await unitOfWork.Repository<TradeBinder>().GetEntityWithSpec(spec, tracking: false);
        if (binder == null)
        {
            logger.LogOperationWarning(operation, "Binder not found", new { id });
            return Results.NotFound();
        }

        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            logger.LogOperationWarning(operation, "Missing user id", new { id });
            return Results.Unauthorized();
        }

        var isOwner = binder.OwnerId == userId;

        if (!isOwner && !binder.IsPublic)
        {
            logger.LogOperationWarning(operation, "Unauthorized", new { id, userId });
            return Results.Unauthorized();
        }

        var marketProvider = await userSettingsService.GetMarketProviderAsync(userId);
        var cards = await unitOfWork.Repository<BinderCard>()
            .ListAsync(new BinderCardsWithBinderIdSpecification(binder.Id), tracking: false) ?? Array.Empty<BinderCard>();
        var pricedCards = MapBinderCardsWithMarketData(cards, marketProvider, userSettingsService, cardDataService);

        logger.LogOperationSuccess(operation, new { id, Cards = cards.Count });
        var dto = MapToDto(binder, cards);
        dto.Cards = pricedCards;
        dto.CardsCount = pricedCards.Count;
        return Results.Ok(dto);
    }

    private static async Task<IResult> GetBinderCardsAsync(
        int binderId,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user,
        [FromServices] IUserSettingsService userSettingsService,
        [FromServices] CardDataService cardDataService,
        [FromServices] ILogger<BindersEndpointLogCategory> logger)
    {
        const string operation = "Binders.Cards.List";
        var (result, binder) = await EnsureBinderAccessAsync(binderId, unitOfWork, user, allowPublic: true, tracking: false);
        if (result != null)
        {
            logger.LogOperationWarning(operation, "Access denied", new { binderId });
            return result;
        }

        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            logger.LogOperationWarning(operation, "Missing user id", new { binderId });
            return Results.Unauthorized();
        }

        var marketProvider = await userSettingsService.GetMarketProviderAsync(userId);
        var cards = await unitOfWork.Repository<BinderCard>()
            .ListAsync(new BinderCardsWithBinderIdSpecification(binder!.Id), tracking: false) ?? Array.Empty<BinderCard>();
        var dto = MapBinderCardsWithMarketData(cards, marketProvider, userSettingsService, cardDataService);

        logger.LogOperationSuccess(operation, new { binderId, Count = dto.Count });
        return Results.Ok(dto);
    }

    private static async Task<IResult> GetBinderCardByIdAsync(
        int binderId,
        int binderCardId,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user,
        [FromServices] IUserSettingsService userSettingsService,
        [FromServices] CardDataService cardDataService,
        [FromServices] ILogger<BindersEndpointLogCategory> logger)
    {
        const string operation = "Binders.Cards.Get";
        var (result, binder) = await EnsureBinderAccessAsync(binderId, unitOfWork, user, allowPublic: true, tracking: false);
        if (result != null)
        {
            logger.LogOperationWarning(operation, "Access denied", new { binderId, binderCardId });
            return result;
        }

        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            logger.LogOperationWarning(operation, "Missing user id", new { binderId, binderCardId });
            return Results.Unauthorized();
        }

        var binderCard = await unitOfWork.Repository<BinderCard>().GetEntityWithSpec(new BinderCardWithBinderSpecification(binderCardId), tracking: false);
        if (binderCard == null || binderCard.TradeBinderId != binder!.Id)
        {
            logger.LogOperationWarning(operation, "Binder card not found", new { binderId, binderCardId });
            return Results.NotFound();
        }

        var marketProvider = await userSettingsService.GetMarketProviderAsync(userId);
        var pricedCard = MapBinderCardsWithMarketData(new[] { binderCard }, marketProvider, userSettingsService, cardDataService).First();
        logger.LogOperationSuccess(operation, new { binderId, binderCardId });
        return Results.Ok(pricedCard);
    }
}
