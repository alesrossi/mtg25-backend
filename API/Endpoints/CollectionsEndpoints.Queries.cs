using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using API.Dtos.Cards;
using API.Dtos.Collections;
using API.Helpers;
using Core.Interfaces;
using Core.Models;
using Core.Specifications;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace API.Endpoints;

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
        HttpContext context)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null) return Results.Unauthorized();

        var collection = await unitOfWork.Repository<Collection>().GetByIdAsync(id);
        if (collection is null) return Results.NotFound();

        return collection.OwnerId != userId ? Results.Unauthorized() : Results.Ok(collection);
    }

    private static async Task<IResult> GetCardsFromCollectionAsync(
        IUnitOfWork unitOfWork,
        int id,
        [AsParameters] EntitySpecParams entityParams,
        HttpContext context)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null) return Results.Unauthorized();

        var collection = await unitOfWork.Repository<Collection>().GetByIdAsync(id);
        if (collection is null) return Results.NotFound();
        if (collection.OwnerId != userId) return Results.Unauthorized();

        if (!string.IsNullOrEmpty(entityParams.GroupBy))
        {
            return await GetGroupedCardsFromCollectionAsync(unitOfWork, id, entityParams);
        }

        var spec = new CardsWithParamsSpecification(entityParams, id);
        var size = await unitOfWork.Repository<Card>().CountAsync(spec);
        var cards = await unitOfWork.Repository<Card>().ListAsync(spec);

        return Results.Ok(new Pagination<Card>(entityParams.PageIndex, entityParams.PageSize, size, cards));
    }

    private static async Task<IResult> GetAllCollectionsForUser(
        IUnitOfWork unitOfWork,
        HttpContext context)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

        var collections = await unitOfWork.Repository<Collection>().ListAsync(new CollectionWithOwnerSpecification(userId));
        return Results.Ok(collections);
    }

    private static async Task<IResult> GetGroupedCardsFromCollectionAsync(
        IUnitOfWork unitOfWork,
        int collectionId,
        EntitySpecParams entityParams)
    {
        var spec = new CardsWithParamsSpecification(entityParams, collectionId);
        var allCards = await unitOfWork.Repository<Card>().ListAsync(spec);

        if (allCards == null || !allCards.Any())
        {
            return Results.Ok(new GroupedCardsPaginationDto
            {
                PageIndex = entityParams.PageIndex,
                PageSize = entityParams.PageSize,
                TotalGroups = 0,
                TotalCards = 0,
                Groups = []
            });
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

        return Results.Ok(result);
    }
}
