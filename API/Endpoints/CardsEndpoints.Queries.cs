using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using API.Dtos.Cards;
using API.Services;
using Core.Interfaces;
using Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace API.Endpoints;

public static partial class CardsEndpoints
{
    private static void MapCardQueries(RouteGroupBuilder group)
    {
        group.MapGet("/{id:int}", GetCardFromId)
            .RequireAuthorization()
            .WithSummary("Get card by ID")
            .WithDescription("Retrieves card by internal database ID")
            .Produces<Card?>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/search/{find}", SearchCards)
            .RequireAuthorization()
            .WithSummary("Search cards by name")
            .WithDescription("Searches cards by name with partial matching")
            .Produces<List<MinimalCardDto>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{name}/versions", GetCardVersionsAsync)
            .RequireAuthorization()
            .WithSummary("Retrieves all versions of a card")
            .WithDescription("Returns all card dtos for a given exact card name")
            .Produces<List<KeyValuePair<string, OracleCardDto>>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/sf/name/{name}", GetCardFromExactName)
            .RequireAuthorization()
            .WithSummary("Returns Scryfall card from name")
            .WithDescription("Returns Scryfall card with all fields, from exact name")
            .Produces<OracleCardDto>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/sf/id/{id}", GetCardFromOracleId)
            .RequireAuthorization()
            .WithSummary("Returns Scryfall card from oracle id")
            .WithDescription("Returns Scryfall card with all fields, from oracle id")
            .Produces<OracleCardDto>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> GetCardFromId(
        IUnitOfWork unit,
        int id,
        CardDataService cds,
        HttpContext context)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null) return Results.Unauthorized();

        var card = await unit.Repository<Card>().GetByIdAsync(id);
        if (card is null) return Results.NotFound();

        var collection = await unit.Repository<Collection>().GetByIdAsync(card.CollectionId);

        return collection!.OwnerId == userId ? Results.Ok(card) : Results.Unauthorized();
    }

    private static IResult SearchCards(
        string find,
        CardDataService cds,
        HttpContext context)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null) return Results.Unauthorized();

        var cardList = cds.CardDataById
            .Where(x => x.Value.Name.Contains(find, StringComparison.OrdinalIgnoreCase))
            .GroupBy(x => x.Value.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        var result = cardList.Select(card =>
        {
            var imageUris = CardDataService.ResolveImageUris(card.Value);
            var imageUrl = imageUris?.Normal ?? imageUris?.Large ?? imageUris?.Png;
            var backImageUrl = cds.ResolveBackImageUrl(card.Value);

            return new MinimalCardDto
            {
                Name = card.Value.Name,
                OracleId = card.Key,
                ImageUrl = imageUrl,
                BackImageUrl = backImageUrl
            };
        }).ToList();
        return result.Count == 0 ? Results.NotFound("Card not found") : Results.Ok(result);
    }

    private static IResult GetCardVersionsAsync(
        string name,
        CardDataService cds,
        HttpContext context)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null) return Results.Unauthorized();

        if (!cds.CardDataByName.ContainsKey(name)) return Results.NotFound();

        var versions = cds.CardDataById
            .Where(x => x.Value.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return Results.Ok(versions);
    }

    private static IResult GetCardFromExactName(
        string name,
        CardDataService cds,
        HttpContext context)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null) return Results.Unauthorized();

        return cds.CardDataByName.TryGetValue(name, out var card) ? Results.Ok(card) : Results.NotFound();
    }

    private static IResult GetCardFromOracleId(
        string id,
        CardDataService cds,
        HttpContext context)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null) return Results.Unauthorized();

        return cds.CardDataById.TryGetValue(id, out var card) ? Results.Ok(card) : Results.NotFound();
    }
}
