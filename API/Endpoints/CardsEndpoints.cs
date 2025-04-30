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
        //app.Services.GetService(typeof(Dictionary<string, OracleCardDto>));
        
        app.MapGet("/cards/{id}", (string id, CardDataService cds) =>
        {
            if (cds.CardData.TryGetValue(id, out var card))
            {
                return Results.Ok(card); // Return the requested card
            }
            return Results.NotFound("Card not found"); // Return 404 if missing
        });

        
        app.MapPost("/cards", async (IUnitOfWork unit, InternalCardDto cardDto) =>
        {
            var collection = await unit.Repository<Collection>().GetByIdAsync(cardDto.CollectionId);
            if (collection == null)
            {
                return Results.NotFound("Collection not found");
            }

            var card = new Card
            {
                Name = cardDto.Name,
                Collection = collection,
                Quantity = cardDto.Quantity,
                Language = cardDto.Language,
                Version = cardDto.Version,
                Condition = Condition.NearMint,
                IsFoil = cardDto.IsFoil,
                PurchasePrice = cardDto.PurchasePrice
            };

            unit.Repository<Card>().Add(card);
            await unit.Complete();
            return Results.Ok(card);
        });
    }
}