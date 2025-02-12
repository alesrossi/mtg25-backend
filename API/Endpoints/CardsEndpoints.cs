using Core.Interfaces;
using Core.Models;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace API.Endpoints;

public static class CardsEndpoints
{
    public static void MapCardsEndpoints(this WebApplication app)
    {

        app.MapGet("/card/{id}", async (MainContext context, int id) =>
        {
            IUnitOfWork unit = new UnitOfWork(context);
            //var context = app.Services.GetRequiredService<MainContext>();
            var card = await unit.Repository<Card>().GetByIdAsync(id);
            if (card == null) return Results.NotFound();
            return Results.Ok(card);
            
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