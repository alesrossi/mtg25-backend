using System;
using System.Security.Claims;
using API.Dtos.Cards;
using API.Services;
using Core.Interfaces;
using Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints;

public static class CardsEndpoints
{
    
    public static void MapCardsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/cards").WithTags("CollectionCards");
        
        group.MapGet("/{id:int}", GetCardFromId)
            .RequireAuthorization()
            .WithSummary("Get card by ID")
            .WithDescription("Retrieves card by internal database ID")
            .Produces<Card?>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
        
        group.MapPut("/{id:int}", UpdateCardFromIdAsync)
            .RequireAuthorization()
            .WithSummary("Update Card")
            .WithDescription("Updates card from form")
            .Produces<Card>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
        
        group.MapDelete("/{id:int}", DeleteCardFromIdAsync)
            .RequireAuthorization()
            .WithSummary("Delete card")
            .WithDescription("Removes card by ID from collection")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
        
        group.MapGet("/search/{find}", SearchCards)
            .RequireAuthorization()
            .WithSummary("Search cards by name")
            .WithDescription("Searches cards by name with partial matching")
            .Produces<List<MinimalCardDto>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
        
        group.MapPost("/", AddNewCardAsync)
            .RequireAuthorization()
            .WithSummary("Add new card")
            .WithDescription("Adds card to collection with properties")
            .Produces<Card>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/card-list", AddCardListAsync)
            .RequireAuthorization()
            .WithSummary("Process card list")
            .WithDescription("Processes card names and returns Oracle card data")
            .Produces<LinkedList<OracleCardDto>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status400BadRequest);
        
        group.MapGet("/{name}/versions", GetCardVersionsAsync)
            .RequireAuthorization()
            .WithSummary("Retrieves all versions of a card")
            .WithDescription("Returns all card dtos for a given exact card name")
            .Produces<List<KeyValuePair<string,OracleCardDto>>>()
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
    
    private static async Task<IResult> UpdateCardFromIdAsync(
        IUnitOfWork unit,
        int id, 
        HttpContext context,
        [FromBody] UpdateCollectionCardDto updateDto)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null) return Results.Unauthorized();

        var card = await unit.Repository<Card>().GetByIdAsync(id);
        if (card is null) return Results.NotFound();
        
        var collection = await unit.Repository<Collection>().GetByIdAsync(card.CollectionId);
        if (collection is null || collection.OwnerId !=  userId) return Results.Unauthorized();
        if (updateDto.Quantity <= 0)
        {
            return Results.BadRequest("Quantity must be greater than zero");
        }

        try
        {
            if (!Enum.TryParse(updateDto.Condition, out Condition condition))
            {
                return Results.BadRequest("Invalid condition");
            }
            card.Collection!.NumberOfCards = card.Collection!.NumberOfCards - card.Quantity + updateDto.Quantity;
            card.CollectionId = updateDto.CollectionId;
            card.Quantity = updateDto.Quantity;
            card.Language = updateDto.Language;
            card.Condition = condition;
            card.IsFoil = updateDto.IsFoil;
            card.PurchasePrice = updateDto.PurchasePrice;
            card.PurchasePriceCurrency = updateDto.PurchasePriceCurrency;
            card.IsMisprint = updateDto.IsMisprint;
            card.IsAltered = updateDto.IsAltered;

            
            
            unit.Repository<Card>().Update(card);
            unit.Repository<Collection>().Update(card.Collection);
            await unit.Complete();
        }
        catch (Exception)
        {
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }
        
        return Results.Ok(card);
    }
    
    private static async Task<IResult> DeleteCardFromIdAsync(
        IUnitOfWork unit,
        int id, 
        CardDataService cds, 
        HttpContext context)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null) return Results.Unauthorized();

        try
        {
            var card = await unit.Repository<Card>().GetByIdAsync(id);
            if (card is null) return Results.NotFound();
            
            var collection = await unit.Repository<Collection>().GetByIdAsync(card.CollectionId);
            if (collection!.OwnerId != userId) return Results.Unauthorized();
            
            unit.Repository<Card>().Delete(card);
            collection.NumberOfCards = Math.Max(0, collection.NumberOfCards - card.Quantity);
            unit.Repository<Collection>().Update(collection);
            await unit.Complete();
            
            return Results.NoContent();
        }
        catch (Exception)
        {
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }
        
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
    
    private static async Task<IResult> AddNewCardAsync (
        IUnitOfWork unit,
        CardDataService cds, 
        HttpContext context, 
        InternalCardDto cardDto)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null) return Results.Unauthorized();
        
        var collection = await unit.Repository<Collection>().GetByIdAsync(cardDto.CollectionId);
        if (collection is null) return Results.NotFound();
        if (collection.OwnerId != userId) return Results.Unauthorized();
        
        if (userId != collection.OwnerId)  return Results.BadRequest("Collection is not valid for logged user");
        
        if (!cds.CardDataById.TryGetValue(cardDto.OracleId, out var oracleCard)) return Results.BadRequest("Card not found for oracleId");

        if (!Enum.TryParse(cardDto.Condition, out Condition myEnum))
        {
            return Results.BadRequest("Invalid condition");
        }
        var imageUris = CardDataService.ResolveImageUris(oracleCard);
        var imageUrl = imageUris.Normal ?? imageUris.Large ?? imageUris.Png ?? throw new InvalidOperationException($"Missing image URL for card {oracleCard.Name}");
        var artCrop = imageUris.ArtCrop ?? throw new InvalidOperationException($"Missing art crop for card {oracleCard.Name}");
        var backImageUrl = cds.ResolveBackImageUrl(oracleCard);

        var card = new Card
        {
            OracleId = oracleCard.Id,
            Name = oracleCard.Name,
            Collection = collection,
            Quantity = cardDto.Quantity,
            Language = cardDto.Language,
            Condition = myEnum,
            IsFoil = cardDto.IsFoil,
            PurchasePrice = cardDto.PurchasePrice,
            ImageUrl = imageUrl,
            PurchasePriceCurrency = cardDto.PurchasePriceCurrency,
            SetCode = oracleCard.Set,
            SetName = oracleCard.SetName,
            CollectorNumber = oracleCard.CollectorNumber!,
            Rarity = oracleCard.Rarity!,
            IsMisprint = cardDto.IsMisprint,
            IsAltered = cardDto.IsAltered,
            ArtCrop = artCrop,
            BackImageUrl = backImageUrl
        };

        unit.Repository<Card>().Add(card);

        collection.NumberOfCards += card.Quantity;
        unit.Repository<Collection>().Update(collection);

        await unit.Complete();
        return Results.Ok(card);
    }
    
    private static Task<IResult> AddCardListAsync (
        CardDataService cds, 
        CardListDto cardListDto, 
        HttpContext context) 
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null) return Task.FromResult(Results.Unauthorized());
        
        var oracleCardList = new LinkedList<OracleCardDto>();
            
        var cardList = cardListDto.CardList.Trim().Split('\n').Select(p => p.Trim());
        foreach (var inputCard in cardList)
        {
            oracleCardList.AddLast(cds.CardDataByName[inputCard]);
        }

        return Task.FromResult(Results.Ok(oracleCardList));
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
    
    private static IResult GetCardVersionsAsync(
        string name, 
        CardDataService cds, 
        HttpContext context)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null) return Results.Unauthorized();
        
        if (!cds.CardDataByName.ContainsKey(name)) return Results.NotFound();
        
        return Results.Ok(cds.CardDataById.Where(x => x.Value.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList());
    }
}
