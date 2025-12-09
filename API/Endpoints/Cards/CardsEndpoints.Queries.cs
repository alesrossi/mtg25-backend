using System.Security.Claims;
using API.Dtos.Cards;
using API.Logging;
using API.Services;
using Core.Interfaces;
using Core.Models;
using Core.Models.Identity;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Endpoints.Cards;

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
            .Produces<List<KeyValuePair<string, ScryfallCardDto>>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/sf/name/{name}", GetCardFromExactName)
            .RequireAuthorization()
            .WithSummary("Returns Scryfall card from name")
            .WithDescription("Returns Scryfall card with all fields, from exact name")
            .Produces<ScryfallCardDto>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/sf/id/{id}", GetCardFromScryfallId)
            .RequireAuthorization()
            .WithSummary("Returns Scryfall card from id")
            .WithDescription("Returns Scryfall card with all fields, from id")
            .Produces<ScryfallCardDto>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> GetCardFromId(
        IUnitOfWork unit,
        int id,
        CardDataService cds,
        HttpContext context,
        [FromServices] AppIdentityDbContext dbContext,
        [FromServices] ILogger<CardsEndpointsLogCategory> logger)
    {
        const string operation = "Cards.GetById";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id", new { id });
            return Results.Unauthorized();
        }

        var card = await unit.Repository<Card>().GetByIdAsync(id, tracking: false);
        if (card is null)
        {
            logger.LogOperationWarning(operation, "Card not found", new { id });
            return Results.NotFound();
        }

        var collection = await unit.Repository<Collection>().GetByIdAsync(card.CollectionId, tracking: false);

        if (collection!.OwnerId != userId)
        {
            logger.LogOperationWarning(operation, "Unauthorized", new { collection.Id, userId });
            return Results.Unauthorized();
        }
        
        var settings = await dbContext.Settings
            .Where(ul => ul.AppUserId == userId)
            .AsNoTracking()
            .FirstOrDefaultAsync();
        
        double? price = null;
        if (settings is not null)
        {
            if (settings.MarketProvider == MarketProvider.Mkm)
            {
                if (card.IsFoil)
                {
                    var eur = cds.CardDataById[card.ScryfallId].Prices!.EurFoil;
                    if (eur != null)
                        price = double.Parse(eur);
                }
                else
                {
                    var eur = cds.CardDataById[card.ScryfallId].Prices!.Eur;
                    if (eur != null)
                        price = double.Parse(eur);
                }
            }
            else
            {
                if (card.IsFoil)
                {
                    var usd = cds.CardDataById[card.ScryfallId].Prices!.UsdFoil;
                    if (usd != null)
                        price = double.Parse(usd);
                }
                else
                {
                    var usd = cds.CardDataById[card.ScryfallId].Prices!.Usd;
                    if (usd != null)
                        price = double.Parse(usd);
                }
            }
        }
        
        logger.LogOperationSuccess(operation, new { id });
        return Results.Ok(CardsEndpointsHelpers.MapToDto(card, price, settings.MarketProvider));
    }

    private static IResult SearchCards(
        string find,
        CardDataService cds,
        HttpContext context,
        [FromServices] ILogger<CardsEndpointsLogCategory> logger)
    {
        const string operation = "Cards.Search";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id");
            return Results.Unauthorized();
        }

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
                ScryfallId = card.Key,
                ImageUrl = imageUrl,
                BackImageUrl = backImageUrl
            };
        }).ToList();
        if (result.Count == 0)
        {
            logger.LogOperationWarning(operation, "No matches", new { find });
            return Results.NotFound("Card not found");
        }

        logger.LogOperationSuccess(operation, new { find, Count = result.Count });
        return Results.Ok(result);
    }

    private static IResult GetCardVersionsAsync(
        string name,
        CardDataService cds,
        HttpContext context,
        [FromServices] ILogger<CardsEndpointsLogCategory> logger)
    {
        const string operation = "Cards.Versions";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id", new { name });
            return Results.Unauthorized();
        }

        if (!cds.CardDataByName.ContainsKey(name))
        {
            logger.LogOperationWarning(operation, "Card not found", new { name });
            return Results.NotFound();
        }

        var versions = cds.CardDataById
            .Where(x => x.Value.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        logger.LogOperationSuccess(operation, new { name, Count = versions.Count });
        return Results.Ok(versions);
    }

    private static IResult GetCardFromExactName(
        string name,
        CardDataService cds,
        HttpContext context,
        [FromServices] ILogger<CardsEndpointsLogCategory> logger)
    {
        const string operation = "Cards.ScryfallByName";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id", new { name });
            return Results.Unauthorized();
        }

        if (cds.CardDataByName.TryGetValue(name, out var card))
        {
            logger.LogOperationSuccess(operation, new { name });
            return Results.Ok(card);
        }

        logger.LogOperationWarning(operation, "Card not found", new { name });
        return Results.NotFound();
    }

    private static IResult GetCardFromScryfallId(
        string id,
        CardDataService cds,
        HttpContext context,
        [FromServices] ILogger<CardsEndpointsLogCategory> logger)
    {
        const string operation = "Cards.ScryfallById";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id", new { id });
            return Results.Unauthorized();
        }

        if (cds.CardDataById.TryGetValue(id, out var card))
        {
            logger.LogOperationSuccess(operation, new { id });
            return Results.Ok(card);
        }

        logger.LogOperationWarning(operation, "Card not found", new { id });
        return Results.NotFound();
    }
}
