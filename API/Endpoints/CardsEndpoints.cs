using System.Text.Json;
using API.Dtos;
using API.Scryfall;
using Core.Interfaces;
using Core.Models;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace API.Endpoints;

public static class CardsEndpoints
{
    
    public static void MapCardsEndpoints(this WebApplication app)
    {
        app.Services.GetService(typeof(Dictionary<string, OracleCardDto>));
        app.MapGet("/generate-bulk", async () =>
        {
            
            return Results.Ok("Generated");
            
        });
        
        app.MapGet("/cards/{id}",  (string id, CardDataService cds) =>
        {
            var cardList = cds.CardData;
            
            return Results.Ok(cardList[id]);
            
        });
        
        app.MapPost("/card", async (MainContext context, Card card) =>
        {
            IUnitOfWork unit = new UnitOfWork(context);
            //var context = app.Services.GetRequiredService<MainContext>();
            unit.Repository<Card>().Add(card);
            return Results.Ok(card);
        });
    }
}