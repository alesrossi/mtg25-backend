using System.Globalization;
using API.Dtos;
using API.Scryfall;
using Core.Interfaces;
using Core.Models;
using Infrastructure.Data;

namespace API.Endpoints;

public static class CardsEndpoints
{
    
    public static void MapCardsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/cards").WithTags("Cards");
        group.MapGet("/{id}", GetCardFromId)
            .WithSummary("Gets card from internal Id")
            .WithDescription("Gets card from DB from internal Id");
        group.MapGet("/search/{find}", SearchCards)
            .WithSummary("Search cards by name")
            .WithDescription("Searches for cards in the database by name using case-insensitive partial matching");
        group.MapPost("/", AddNewCardAsync)
            .WithSummary("Add a new card")
            .WithDescription("Adds a new card to a collection with specified properties like condition, language, and pricing");
        group.MapPost("/card-list", AddCardListAsync)
            .WithSummary("Process card list")
            .WithDescription("Processes a list of card names and returns corresponding Oracle card data");
    }
    
    private static IResult GetCardFromId(string id, CardDataService cds)
    {
        if (cds.CardDataById.TryGetValue(id, out var card))
        {
            return Results.Ok(card); // Return the requested card
        }
        return Results.NotFound("Card not found"); // Return 404 if missing
    }
    
    private static IResult SearchCards(string find, CardDataService cds)
    {
        var result = new List<MinimalCardDto>();
        var cardList = cds.CardDataById.Where(x => x.Value.Name.IndexOf(find, StringComparison.OrdinalIgnoreCase) >= 0);

        foreach (var card in cardList)
        {
            result.Add(new MinimalCardDto
            {
                Name = card.Value.Name,
                OracleId = card.Key,
                ImageUrl = card.Value.ImageUris?.Normal
            });
        }
        if (!result.Any()) return Results.NotFound("Card not found");
        return Results.Ok(result);
    }
    
    private static async Task<IResult> AddNewCardAsync (IUnitOfWork unit, CardDataService cds, InternalCardDto cardDto)
    {
        var collection = await unit.Repository<Collection>().GetByIdAsync(cardDto.CollectionId);

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
    
    private static async Task<IResult> AddCardListAsync (CardDataService cds, CardListDto cardListDto) 
    {
        var oracleCardList = new LinkedList<OracleCardDto>();
            
        var cardList = cardListDto.CardList.Trim().Split('\n').Select(p => p.Trim());
        foreach (var inputCard in cardList)
        {
            oracleCardList.AddLast(cds.CardDataByName[inputCard]);
        }

        return Results.Ok(oracleCardList);
    }
}