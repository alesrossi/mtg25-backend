using System;
using System.Security.Claims;
using API.Dtos.Cards;
using API.Services;
using Core.Interfaces;
using Core.Models;

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
        // group.MapPut("/{id}", UpdateCardFromIdAsync)
        //     .RequireAuthorization()
        //     .WithSummary("Update Card")
        //     .WithDescription("Updates card from form");
        
        group.MapDelete("/{id:int}", DeleteCardFromIdAsync)
            .RequireAuthorization()
            .WithSummary("Delete card")
            .WithDescription("Removes card by ID from collection")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
        
        group.MapGet("/search/{find}", SearchCards)
            .RequireAuthorization()
            .WithSummary("Search cards by name")
            .WithDescription("Searches cards by name with partial matching")
            .Produces<List<MinimalCardDto>>()
            .Produces(StatusCodes.Status401Unauthorized);
        
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
            .Produces<List<OracleCardDto>>()
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
    
    // private static IResult UpdateCardFromIdAsync(
    //     IUnitOfWork unit,
    //     string id, 
    //     CardDataService cds, 
    //     HttpContext context)
    // {
    //     var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    //     if (userId is null) return Results.Unauthorized();
    //     
    //     try
    //     {
    //         var card = await unit.Repository<Card>().GetByIdAsync(id);
    //         unit.Repository<Card>().Delete(card);
    //         await unit.Complete();
    //         return Results.Ok();
    //     }
    //     catch (Exception e)
    //     {
    //         return Results.BadRequest(e.Message);
    //     }
    // }
    
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
            await unit.Complete();
            
            return Results.Ok();
        }
        catch (Exception e)
        {
            return Results.BadRequest(e.Message);
        }
        
    }
    
    private static IResult SearchCards(
        string find, 
        CardDataService cds, 
        HttpContext context)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null) return Results.Unauthorized();

        var cardList = cds.CardDataById.Where(x => x.Value.Name.Contains(find, StringComparison.OrdinalIgnoreCase));

        var result = cardList.Select(card =>
        {
            var imageUris = cds.ResolveImageUris(card.Value);
            var imageUrl = imageUris?.Normal ?? imageUris?.Large ?? imageUris?.Png;

            return new MinimalCardDto
            {
                Name = card.Value.Name,
                OracleId = card.Key,
                ImageUrl = imageUrl
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
        
        var oracleCard = cds.CardDataById[cardDto.OracleId];

        if (!Enum.TryParse(cardDto.Condition, out Condition myEnum))
        {
            return Results.BadRequest("Invalid condition");
        }
        var imageUris = cds.ResolveImageUris(oracleCard) ?? throw new InvalidOperationException($"Missing image data for card {oracleCard.Name}");
        var imageUrl = imageUris.Normal ?? imageUris.Large ?? imageUris.Png ?? throw new InvalidOperationException($"Missing image URL for card {oracleCard.Name}");
        var artCrop = imageUris.ArtCrop ?? throw new InvalidOperationException($"Missing art crop for card {oracleCard.Name}");

        var card = new Card
        {
            OracleId = oracleCard.Id,
            Name = oracleCard.Name,
            Collection = collection,
            Quantity = cardDto.Quantity,
            Language = cardDto.Language,
            Version = cardDto.Version,
            Condition = myEnum,
            IsFoil = cardDto.IsFoil,
            PurchasePrice = cardDto.PurchasePrice,
            ImageUrl = imageUrl,
            PurchasePriceCurrency = cardDto.PurchasePriceCurrency,
            SetCode = oracleCard.SetId!,
            SetName = oracleCard.SetName,
            CollectorNumber = oracleCard.CollectorNumber!,
            Rarity = oracleCard.Rarity!,
            IsMisprint = cardDto.IsMisprint,
            IsAltered = cardDto.IsAltered,
            ArtCrop = artCrop
        };

        unit.Repository<Card>().Add(card);
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
