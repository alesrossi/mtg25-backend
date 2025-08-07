using Core.Interfaces;
using Core.Models;

namespace API.Endpoints;

public static class DecksEndpoint
{
    public static void MapDecksEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/decks").WithTags("Decks");
        group.MapGet("/", GetAllDecksForUser)
            .WithSummary("Get decks for user")
            .WithDescription("Gets all decks from a given user");
        // group.MapPost("/", AddNewDeckAsync)
        //     .WithSummary("Add a new deck")
        //     .WithDescription("Adds a new deck to a user");
        // group.MapGet("/print-deck-list", PrintDeckList)
        //     .WithSummary("Print deck list")
        //     .WithDescription("Print deck list");
    }
    
    private static async Task<IResult> GetAllDecksForUser(IUnitOfWork unit)
    {
        var decks = await unit.Repository<Deck>().ListAllAsync();
        return decks.Count <= 0 ? Results.NotFound("No decks found") : Results.Ok(decks);
    }
}