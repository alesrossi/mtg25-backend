using System.Globalization;
using System.Security.Claims;
using API.Dtos.Cards;
using API.Endpoints.Cards;
using API.Helpers;
using API.Logging;
using API.Services;
using Core.Interfaces;
using Core.Models;
using Core.Models.Identity;
using Core.Specifications;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints.Collections;

public static partial class CollectionsEndpoints
{
    private static async Task<IResult> GetCollectionFromIdAsync(
        IUnitOfWork unitOfWork,
        int id,
        HttpContext context,
        [FromServices] ILogger<CollectionsEndpointLogCategory> logger)
    {
        const string operation = "Collections.GetById";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id", new { id });
            return Results.Unauthorized();
        }

        var collection = await unitOfWork.Repository<Collection>().GetByIdAsync(id, tracking: false);
        if (collection is null)
        {
            logger.LogOperationWarning(operation, "Collection not found", new { id });
            return Results.NotFound();
        }

        if (collection.OwnerId != userId)
        {
            logger.LogOperationWarning(operation, "Unauthorized", new { id, userId });
            return Results.Unauthorized();
        }

        logger.LogOperationSuccess(operation, new { id });
        return Results.Ok(collection);
    }

    private static async Task<IResult> GetCardsFromCollectionAsync(
        IUnitOfWork unitOfWork,
        int id,
        CardDataService cds,
        [AsParameters] EntitySpecParams entityParams,
        HttpContext context,
        [FromServices] IUserSettingsService userSettingsService,
        [FromServices] ILogger<CollectionsEndpointLogCategory> logger)
    {
        const string operation = "Collections.GetCards";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id", new { id });
            return Results.Unauthorized();
        }

        var collection = await unitOfWork.Repository<Collection>().GetByIdAsync(id, tracking: false);
        if (collection is null)
        {
            logger.LogOperationWarning(operation, "Collection not found", new { id });
            return Results.NotFound();
        }
        if (collection.OwnerId != userId)
        {
            logger.LogOperationWarning(operation, "Unauthorized", new { id, userId });
            return Results.Unauthorized();
        }

        if (!string.IsNullOrEmpty(entityParams.GroupBy))
        {
            var grouped = await GetGroupedCardsFromCollectionAsync(unitOfWork, id, entityParams, logger);
            logger.LogOperationSuccess(operation, new { id, entityParams.GroupBy, Grouped = true });
            return grouped;
        }

        var sortByCurrentPrice = SortsByCurrentPrice(entityParams.Sort);
        var listingSpec = sortByCurrentPrice
            ? new CardsWithParamsSpecification(entityParams, id, applySorting: false, applyPaging: false)
            : new CardsWithParamsSpecification(entityParams, id);

        var cards = await unitOfWork.Repository<Card>().ListAsync(listingSpec, tracking: false) ?? Array.Empty<Card>();

        var marketProvider = await userSettingsService.GetMarketProviderAsync(userId);
        var mappedCards = new List<ExtensiveCardDto>(cards.Count);
        foreach (var card in cards)
        {
            var (price, resolvedProvider) = ResolveMarketPrice(card, cds, marketProvider);
            mappedCards.Add(CardsEndpointsHelpers.MapToDto(card, price, resolvedProvider));
        }

        int size;
        if (sortByCurrentPrice)
        {
            var skip = entityParams.PageSize * (entityParams.PageIndex - 1);
            mappedCards = SortByCurrentPrice(mappedCards, entityParams.Sort)
                .Skip(skip)
                .Take(entityParams.PageSize)
                .ToList();
            size = cards.Count;
        }
        else
        {
            size = await unitOfWork.Repository<Card>().CountAsync(listingSpec);
        }
        
        logger.LogOperationSuccess(operation, new { id, entityParams.PageIndex, entityParams.PageSize, entityParams.Sort, Count = mappedCards.Count });
        return Results.Ok(new Pagination<ExtensiveCardDto>(entityParams.PageIndex, entityParams.PageSize, size, mappedCards));
    }

    private static async Task<IResult> GetAllCollectionsForUser(
        IUnitOfWork unitOfWork,
        HttpContext context,
        [FromServices] ILogger<CollectionsEndpointLogCategory> logger)
    {
        const string operation = "Collections.List";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            logger.LogOperationWarning(operation, "Missing user id");
            return Results.Unauthorized();
        }

        var collections = await unitOfWork.Repository<Collection>().ListAsync(new CollectionWithOwnerSpecification(userId), tracking: false);
        logger.LogOperationSuccess(operation, new { userId, Count = collections?.Count });
        return Results.Ok(collections);
    }

    private static async Task<IResult> GetGroupedCardsFromCollectionAsync(
        IUnitOfWork unitOfWork,
        int collectionId,
        EntitySpecParams entityParams,
        ILogger<CollectionsEndpointLogCategory> logger)
    {
        var spec = new CardsWithParamsSpecification(entityParams, collectionId);
        var allCards = await unitOfWork.Repository<Card>().ListAsync(spec, tracking: false);

        if (allCards == null || !allCards.Any())
        {
            var empty = new GroupedCardsPaginationDto
            {
                PageIndex = entityParams.PageIndex,
                PageSize = entityParams.PageSize,
                TotalGroups = 0,
                TotalCards = 0,
                Groups = []
            };
            logger.LogOperationWarning("Collections.GetCards", "No cards to group", new { collectionId });
            return Results.Ok(empty);
        }

        var groupedCards = entityParams.GroupBy?.ToLower() switch
        {
            "setname" => allCards.GroupBy(c => c.SetName).Select(g => new GroupedCardsDto
            {
                GroupKey = g.Key,
                Count = g.Count(),
                Cards = g.ToList()
            }).ToList(),
            "setcode" => allCards.GroupBy(c => c.SetCode).Select(g => new GroupedCardsDto
            {
                GroupKey = g.Key,
                Count = g.Count(),
                Cards = g.ToList()
            }).ToList(),
            "rarity" => allCards.GroupBy(c => c.Rarity).Select(g => new GroupedCardsDto
            {
                GroupKey = g.Key,
                Count = g.Count(),
                Cards = g.ToList()
            }).ToList(),
            "condition" => allCards.GroupBy(c => c.Condition.ToString()).Select(g => new GroupedCardsDto
            {
                GroupKey = g.Key,
                Count = g.Count(),
                Cards = g.ToList()
            }).ToList(),
            "language" => allCards.GroupBy(c => c.Language).Select(g => new GroupedCardsDto
            {
                GroupKey = g.Key,
                Count = g.Count(),
                Cards = g.ToList()
            }).ToList(),
            _ => allCards.GroupBy(c => c.Name).Select(g => new GroupedCardsDto
            {
                GroupKey = g.Key,
                Count = g.Count(),
                Cards = g.ToList()
            }).ToList()
        };

        var totalGroups = groupedCards.Count;
        var totalCards = allCards.Count;

        var paginatedGroups = groupedCards
            .Skip(entityParams.PageSize * (entityParams.PageIndex - 1))
            .Take(entityParams.PageSize)
            .ToList();

        var result = new GroupedCardsPaginationDto
        {
            PageIndex = entityParams.PageIndex,
            PageSize = entityParams.PageSize,
            TotalGroups = totalGroups,
            TotalCards = totalCards,
            Groups = paginatedGroups
        };

        logger.LogOperationSuccess("Collections.GetCards", new { collectionId, entityParams.GroupBy, Groups = paginatedGroups.Count });
        return Results.Ok(result);
    }

    private static bool SortsByCurrentPrice(string? sort) =>
        string.Equals(sort, "currentPriceAsc", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(sort, "currentPriceDesc", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<ExtensiveCardDto> SortByCurrentPrice(IEnumerable<ExtensiveCardDto> cards, string? sort)
    {
        var ordered = cards.OrderBy(c => c.Price.HasValue ? 0 : 1);
        var descending = string.Equals(sort, "currentPriceDesc", StringComparison.OrdinalIgnoreCase);
        return descending
            ? ordered.ThenByDescending(c => c.Price ?? double.MinValue)
            : ordered.ThenBy(c => c.Price ?? double.MaxValue);
    }

    private static (double? price, MarketProvider? provider) ResolveMarketPrice(Card card, CardDataService cds, MarketProvider preferredProvider)
    {
        if (!cds.CardDataById.TryGetValue(card.ScryfallId, out var marketData) || marketData?.Prices is null)
        {
            return (null, preferredProvider);
        }

        var prices = marketData.Prices;
        foreach (var provider in EnumerateProviders(preferredProvider))
        {
            var selected = provider == MarketProvider.Mkm
                ? (card.IsFoil ? prices.EurFoil : prices.Eur)
                : (card.IsFoil ? prices.UsdFoil : prices.Usd);

            var parsed = TryParsePrice(selected);
            if (parsed.HasValue)
            {
                return (parsed, provider);
            }
        }

        return (null, preferredProvider);
    }

    private static IEnumerable<MarketProvider> EnumerateProviders(MarketProvider preferred)
    {
        yield return preferred;
        yield return preferred == MarketProvider.Mkm ? MarketProvider.Tcg : MarketProvider.Mkm;
    }

    private static double? TryParsePrice(string? value)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }
}
