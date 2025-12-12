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
using Infrastructure.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Endpoints.Collections;

public static partial class CollectionsEndpoints
{
    private static void MapCollectionQueries(RouteGroupBuilder group)
    {
        group.MapGet("/{id:int}", GetCollectionFromIdAsync)
            .RequireAuthorization()
            .WithSummary("Get collection by ID")
            .WithDescription("Retrieves specific collection by ID")
            .Produces<Collection>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{id:int}/cards", GetCardsFromCollectionAsync)
            .RequireAuthorization()
            .WithSummary("Get cards from collection")
            .WithDescription("Retrieves paginated list of cards from a specific collection with filtering, sorting, searching, and optional grouping")
            .Produces<Pagination<Card>>()
            .Produces<GroupedCardsPaginationDto>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/", GetAllCollectionsForUser)
            .RequireAuthorization()
            .WithSummary("Get user's collections")
            .WithDescription("Returns all collections owned by authenticated user")
            .Produces<List<Collection>>()
            .Produces(StatusCodes.Status401Unauthorized);
    }

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
        [FromServices] AppIdentityDbContext dbContext,
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

        var spec = new CardsWithParamsSpecification(entityParams, id);
        var size = await unitOfWork.Repository<Card>().CountAsync(spec);
        var cards = await unitOfWork.Repository<Card>().ListAsync(spec, tracking: false);

        var settings = await dbContext.Settings
            .Where(ul => ul.AppUserId == userId)
            .AsNoTracking()
            .FirstOrDefaultAsync();
        
        List<ExtensiveCardDto> cardList = [];
        var marketProvider = settings?.MarketProvider ?? MarketProvider.Mkm;
        foreach (var card in cards)
        {
            double? price = null;
            if (settings is not null && cds.CardDataById.TryGetValue(card.ScryfallId, out var marketData) && marketData?.Prices is not null)
            {
                if (settings.MarketProvider == MarketProvider.Mkm)
                {
                    if (card.IsFoil)
                    {
                        var eur = marketData.Prices!.EurFoil;
                        if (eur != null)
                            price = double.Parse(eur);
                    }
                    else
                    {
                        var eur = marketData.Prices!.Eur;
                        if (eur != null)
                            price = double.Parse(eur);
                    }
                }
                else
                {
                    if (card.IsFoil)
                    {
                        var usd = marketData.Prices!.UsdFoil;
                        if (usd != null)
                            price = double.Parse(usd);
                    }
                    else
                    {
                        var usd = marketData.Prices!.Usd;
                        if (usd != null)
                            price = double.Parse(usd);
                    }
                }
            }
            
            cardList.Add(CardsEndpointsHelpers.MapToDto(card, price, marketProvider));
        }
        
        logger.LogOperationSuccess(operation, new { id, entityParams.PageIndex, entityParams.PageSize, Count = cards?.Count });
        return Results.Ok(new Pagination<ExtensiveCardDto>(entityParams.PageIndex, entityParams.PageSize, size, cardList));
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
}
