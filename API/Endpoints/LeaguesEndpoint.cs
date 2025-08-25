using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using API.Dtos;
using Core.Models.Identity;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Endpoints;

public static class LeaguesEndpoint
{
    public static void MapLeaguesEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/leagues").WithTags("Leagues");
        
        group.MapGet("/user", GetLeaguesFromUserAsync)
            .WithSummary("Returns all leagues for user")
            .WithDescription("Returns all leagues for user given JWT token")
            .Produces<List<MinimalCardDto>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        // group.MapGet("/", GetLeaguesAsync)
        //     .WithSummary("Returns all leagues")
        //     .WithDescription("Returns all leagues with filtering, sorting and pagination")
        //     .Produces<List<LeagueDto>>()
        //     .Produces(StatusCodes.Status401Unauthorized);
        
        group.MapGet("/{id:int}", GetLeagueFromIdAsync)
            .WithSummary("Returns all leagues")
            .WithDescription("Returns all leagues with filtering, sorting and pagination")
            .Produces<List<League>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
        
        group.MapPut("/{id:int}", UpdateLeagueAsync)
            .WithSummary("Returns all leagues")
            .WithDescription("Returns all leagues with filtering, sorting and pagination")
            .Produces<List<LeagueDto>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
        
        group.MapPatch("/{id:int}/results", UpdateLeagueFromResultsAsync)
            .WithSummary("Returns all leagues")
            .WithDescription("Returns all leagues with filtering, sorting and pagination")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
        
        group.MapGet("/{id:int}/invite", GetInviteCodeAsync)
            .WithSummary("Returns all leagues")
            .WithDescription("Returns all leagues with filtering, sorting and pagination")
            .Produces<string>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
    }
    
    private static async Task<IResult> GetLeaguesFromUserAsync(
        [FromServices] UserManager<AppUser> userManager, 
        [FromServices] AppIdentityDbContext  dbContext,
        HttpContext context)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null) return Results.Unauthorized();
        
        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return Results.Unauthorized();
        
        var res = await dbContext.UserLeagues
            .Where(ul => ul.UserId == userId)
            .Include(ul => ul.League)
            .ToListAsync();

        List<LeagueDto> leaguesDto = [];
        res.ForEach(x => 
            leaguesDto.Add(new LeagueDto
            {
                Code = x.League.Code, 
                Name = x.League.Name, 
                Score = x.Score, 
                Id = x.LeagueId,
                //EndDate = x.League.EndDate,
                Format = x.League.Format,
            }));
        
        return Results.Ok(new UserWithLeaguesDto
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            DisplayName = user.DisplayName,
            Leagues = leaguesDto
        });
    }
    
    private static async Task<IResult> GetLeagueFromIdAsync(
        int id,
        [FromServices] UserManager<AppUser> userManager, 
        [FromServices] AppIdentityDbContext  dbContext,
        [FromBody] List<UserDto> users,
        HttpContext context)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null) return Results.Unauthorized();
        
        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return Results.Unauthorized();
        
        var league = await dbContext.FindAsync<League>(id);
        if (league is null) return Results.NotFound();
        
        var res = await dbContext.UserLeagues
            .Where(ul => ul.LeagueId == league.Id && ul.UserId == userId)
            .Include(ul => ul.League)
            .FirstOrDefaultAsync();

        return res is null ? Results.Unauthorized() : Results.Ok(league);
    }
    
    private static async Task<IResult> UpdateLeagueAsync(
        int id,
        [FromServices] UserManager<AppUser> userManager, 
        [FromServices] AppIdentityDbContext  dbContext,
        HttpContext context)
    {
        return Results.Ok();
    }
    
    private static async Task<IResult> UpdateLeagueFromResultsAsync(
        int id,
        [FromBody] List<UserWithScore> userList,
        [FromServices] UserManager<AppUser> userManager, 
        [FromServices] AppIdentityDbContext  dbContext,
        HttpContext context)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null) return Results.Unauthorized();
        
        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return Results.Unauthorized();
        
        var league = await dbContext.FindAsync<League>(id);
        if (league is null || league.OwnerId != userId) return Results.NotFound();
        if (league.OwnerId != userId) return Results.Unauthorized();
        
        var res = await dbContext.UserLeagues
            .Where(ul => ul.LeagueId == league.Id)
            .Include(ul => ul.User)
            .ToListAsync();
        
        res.ForEach(x =>
        {
            foreach (var userWithScore in userList.Where(userWithScore => userWithScore.UserId == x.UserId))
            {
                x.Score = +userWithScore.Score;
                x.BestRound = userWithScore.Score > x.BestRound ? userWithScore.Score : x.BestRound;
                x.RoundsPlayed = x.RoundsPlayed++;
                x.Rounds.Add(userWithScore.Score);
                x.AvgScore = x.Rounds.Average();
            }
        });
        await dbContext.AddRangeAsync(res);
        await dbContext.SaveChangesAsync();
        
        return Results.Ok();
    }
    
    private static async Task<IResult> GetInviteCodeAsync(
        int id,
        [FromServices] UserManager<AppUser> userManager, 
        [FromServices] AppIdentityDbContext  dbContext,
        HttpContext context)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null) return Results.Unauthorized();
        
        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return Results.Unauthorized();
        
        var league = await dbContext.FindAsync<League>(id);
        if (league is null || league.OwnerId != userId) return Results.NotFound();
        
        return league.OwnerId != userId ? Results.Unauthorized() : Results.Ok(league.Code);
    }
    
    private static class SecureCodeGenerator
    {
        private static readonly char[] chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray();
    
        public static string GenerateCode(int length = 6)
        {
            var data = new byte[length];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(data);
            }

            var result = new StringBuilder(length);
            foreach (var b in data)
            {
                result.Append(chars[b % chars.Length]);
            }

            return result.ToString();
        }
    }
}