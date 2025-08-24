using System.Security.Claims;
using API.Dtos;
using Core.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Endpoints;

public static class LeaguesEndpoint
{
    public static void MapLeaguesEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/leagues").WithTags("Leagues");
        group.MapGet("/user", GetLeaguesFromUser)
            .WithSummary("Returns all leagues for user")
            .WithDescription("Returns all leagues for user given JWT token");
    }
    
    private static async Task<IResult> GetLeaguesFromUser(
        [FromServices] UserManager<AppUser> userManager, 
        HttpContext context)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null) return Results.Unauthorized();
        
        var user = await userManager.Users.Include(u => u.Leagues)
            .FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return Results.NotFound();

        List<LeagueDto> leagues = [];
        user.Leagues.ForEach(l => leagues.Add(new LeagueDto { Id = l.Id, Name = l.Name }));
        
        return Results.Ok(new UserWithLeaguesDto
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            DisplayName = user.DisplayName,
            Leagues = leagues
        });
    }
}