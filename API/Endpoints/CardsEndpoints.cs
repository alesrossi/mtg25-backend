using System.Security.Claims;
using API.Dtos;
using API.Services;
using Core.Interfaces;
using Core.Models;

namespace API.Endpoints;

public static class CardsEndpoints
{
    
    public static void MapCardsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/cards").WithTags("Cards");
        group.MapGet("/{id}", GetCardFromId)
            .RequireAuthorization()
            .WithSummary("Gets card from internal Id")
            .WithDescription("Gets card from DB from internal Id");
        group.MapGet("/search/{find}", SearchCards)
            .RequireAuthorization()
            .WithSummary("Search cards by name")
            .WithDescription("Searches for cards in the database by name using case-insensitive partial matching");
        group.MapPost("/", AddNewCardAsync)
            .RequireAuthorization()
            .WithSummary("Add a new card")
            .WithDescription("Adds a new card to a collection with specified properties like condition, language, and pricing")
            .RequireAuthorization();
        group.MapPost("/card-list", AddCardListAsync)
            .RequireAuthorization()
            .WithSummary("Process card list")
            .WithDescription("Processes a list of card names and returns corresponding Oracle card data");
    }
    
    private static IResult GetCardFromId(
        string id, 
        CardDataService cds, 
        HttpContext context)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null) return Results.Unauthorized();
        
        return cds.CardDataById.TryGetValue(id, out var card) ? Results.Ok(card) : // Return the requested card
            Results.NotFound("Card not found"); // Return 404 if missing
    }
    
    private static IResult SearchCards(
        string find, 
        CardDataService cds, 
        HttpContext context)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null) return Results.Unauthorized();

        var cardList = cds.CardDataById.Where(x => x.Value.Name.IndexOf(find, StringComparison.OrdinalIgnoreCase) >= 0);

        var result = cardList.Select(card => new MinimalCardDto { Name = card.Value.Name, OracleId = card.Key, ImageUrl = card.Value.ImageUris?.Normal }).ToList();
        return result.Count == 0 ? Results.NotFound("Card not found") : Results.Ok(result);
    }
    
    private static async Task<IResult> AddNewCardAsync (IUnitOfWork unit,
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
            ImageUrl = oracleCard.ImageUris.Normal,
            PurchasePriceCurrency = cardDto.PurchasePriceCurrency,
            SetCode = oracleCard.SetId,
            SetName = oracleCard.SetName,
            CollectorNumber = oracleCard.CollectorNumber,
            Rarity = oracleCard.Rarity,
            IsMisprint = cardDto.IsMisprint,
            IsAltered = cardDto.IsAltered,

        };

        unit.Repository<Card>().Add(card);
        await unit.Complete();
        return Results.Ok(card);
    }
    
    private static async Task<IResult> AddCardListAsync (
        CardDataService cds, 
        CardListDto cardListDto, 
        HttpContext context) 
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null) return Results.Unauthorized();
        
        var oracleCardList = new LinkedList<OracleCardDto>();
            
        var cardList = cardListDto.CardList.Trim().Split('\n').Select(p => p.Trim());
        foreach (var inputCard in cardList)
        {
            oracleCardList.AddLast(cds.CardDataByName[inputCard]);
        }

        return Results.Ok(oracleCardList);
    }
}