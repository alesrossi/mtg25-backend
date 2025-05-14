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
        app.MapGet("/cards/{id}", (string id, CardDataService cds) =>
        {
            if (cds.CardDataById.TryGetValue(id, out var card))
            {
                return Results.Ok(card); // Return the requested card
            }
            return Results.NotFound("Card not found"); // Return 404 if missing
        });
        
        app.MapGet("/cards/search/{find}", (string find, CardDataService cds) =>
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
        });

        
        app.MapPost("/cards", async (IUnitOfWork unit, CardDataService cds, InternalCardDto cardDto) =>
        {
            var collection = await unit.Repository<Collection>().GetByIdAsync(cardDto.CollectionId);
            if (collection == null)
            {
                return Results.NotFound("Collection not found");
            }
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
            };

            unit.Repository<Card>().Add(card);
            await unit.Complete();
            return Results.Ok(card);
        });
    }
}