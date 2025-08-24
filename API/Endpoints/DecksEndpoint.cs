using System.Security.Claims;
using Core.Interfaces;
using Core.Models;
using Core.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints;

public static class DecksEndpoint
{
    public static void MapDecksEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/decks").WithTags("Decks");
        group.MapGet("/", GetAllDecksForUser)
            .WithSummary("Get decks for user")
            .WithDescription("Gets all decks from a given user")
            .Produces<IReadOnlyList<Deck>?>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
        // group.MapPost("/", AddNewDeckAsync)
        //     .WithSummary("Add a new deck")
        //     .WithDescription("Adds a new deck to a user");
        // group.MapGet("/print-deck-list", PrintDeckList)
        //     .WithSummary("Print deck list")
        //     .WithDescription("Print deck list");
    }
    
    private static async Task<IResult> GetAllDecksForUser(
        IUnitOfWork unit,
        [FromServices] UserManager<AppUser> userManager,
        HttpContext context)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null) return Results.Unauthorized();
        
        var decks = await unit.Repository<Deck>().ListAllAsync();
        return decks is null || decks.Count <= 0 ? Results.NotFound("No decks found") : Results.Ok(decks);
    }
}