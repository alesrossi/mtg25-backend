using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using API.Dtos;
using API.Services;
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
            .RequireAuthorization()
            .WithSummary("Returns all leagues for user")
            .WithDescription("Returns all leagues for user given JWT token")
            .Produces<List<MinimalCardDto>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/", GetLeaguesAsync)
            .WithSummary("Returns all leagues")
            .WithDescription("Returns all leagues with filtering, sorting and pagination")
            .Produces<List<League>>()
            .Produces(StatusCodes.Status401Unauthorized);
        
        group.MapGet("/{id:int}", GetLeagueFromIdAsync)
            .RequireAuthorization()
            .WithSummary("Returns all leagues")
            .WithDescription("Returns all leagues with filtering, sorting and pagination")
            .Produces<List<League>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
        
        group.MapPut("/{id:int}", UpdateLeagueAsync)
            .RequireAuthorization()
            .WithSummary("Returns all leagues")
            .WithDescription("Returns all leagues with filtering, sorting and pagination")
            .Produces<List<LeagueDto>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
        
        group.MapPatch("/{id:int}/results", UpdateLeagueFromResultsAsync)
            .RequireAuthorization()
            .WithSummary("Returns all leagues")
            .WithDescription("Returns all leagues with filtering, sorting and pagination")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
        
        group.MapGet("/{id:int}/invite", GetInviteCodeAsync)
            .RequireAuthorization()
            .WithSummary("Returns all leagues")
            .WithDescription("Returns all leagues with filtering, sorting and pagination")
            .Produces<string>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
        
        group.MapPatch("/{code}/join", JoinLeagueFromCodeAsync)
            .RequireAuthorization()
            .WithSummary("Returns all leagues")
            .WithDescription("Returns all leagues with filtering, sorting and pagination")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
        
        group.MapPost("/", CreateNewLeagueAsync)
            .RequireAuthorization()
            .WithSummary("Creates a new League")
            .WithDescription("Creates a new league given all the options")
            .Produces<League>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status400BadRequest) ;
    }
    
    private static async Task<IResult> GetLeaguesAsync(
        [FromServices] UserManager<AppUser> userManager, 
        [FromServices] AppIdentityDbContext  dbContext,
        HttpContext context)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null) return Results.Unauthorized();
        
        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return Results.Unauthorized();

        var leagues = await dbContext.Leagues.ToListAsync();
        return Results.Ok(leagues);
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
                Format = x.League.Format,
                TotalRounds = x.League.TotalRounds,
                RoundsToConsider = x.League.RoundsToConsider,
                MinimumRounds = x.League.MinimumRounds,
                TotalPrize = x.League.TotalPrize,
                PrizePerPerson = x.League.PrizePerPerson,
                TotalPlayers = x.League.TotalPlayers,
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

        if (res is null) return Results.Unauthorized();
        
        var leagueDto = new LeagueDto
        {
            Id = league.Id,
            Name = league.Name,
            Code = league.Code,
            Format = league.Format,
            TotalRounds = league.TotalRounds,
            RoundsToConsider = league.RoundsToConsider,
            MinimumRounds = league.MinimumRounds,
            TotalPrize = league.TotalPrize,
            PrizePerPerson = league.PrizePerPerson,
            TotalPlayers = league.TotalPlayers,
            Score = res.Score
        };
        
        return Results.Ok(leagueDto);
    }
    
    private static async Task<IResult> UpdateLeagueAsync(
        int id,
        [FromServices] UserManager<AppUser> userManager, 
        [FromServices] AppIdentityDbContext  dbContext,
        [FromBody] UpdateLeagueDto updateLeague,
        HttpContext context)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null) return Results.Unauthorized();
        
        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return Results.Unauthorized();
        
        var league = await dbContext.FindAsync<League>(id);
        if (league is null) return Results.NotFound();
        if (league.OwnerId != user.Id) return Results.Unauthorized();
        
        if (updateLeague.Name != null) league.Name = updateLeague.Name;
        if (updateLeague.TotalRounds != null) league.TotalRounds = (int)updateLeague.TotalRounds;
        if (updateLeague.RoundsToConsider != null) league.RoundsToConsider = (int)updateLeague.RoundsToConsider;
        if (updateLeague.MinimumRounds != null) league.MinimumRounds = (int)updateLeague.MinimumRounds;
        if (updateLeague.TotalPrize != null) league.TotalPrize = (double)updateLeague.TotalPrize;
        if (updateLeague.PrizePerPerson != null) league.PrizePerPerson = (double)updateLeague.PrizePerPerson;
        
        dbContext.Update(league);
        await dbContext.SaveChangesAsync();
        
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
                league.TotalPrize += league.PrizePerPerson;
            }
        });
        
        dbContext.Update(league);
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
    
    private static async Task<IResult> JoinLeagueFromCodeAsync(
        string code,
        [FromServices] UserManager<AppUser> userManager, 
        [FromServices] AppIdentityDbContext  dbContext,
        HttpContext context)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null) return Results.Unauthorized();
        
        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return Results.Unauthorized();
        
        var league = await dbContext.Set<League>().Where(x => x.Code == code).FirstOrDefaultAsync();
        if (league is null) return Results.NotFound("League not found");
        league.TotalPlayers++;
        
        await dbContext.AddAsync(new AppUserLeague
        {
            UserId = user.Id,
            User = user,
            LeagueId = league.Id,
            League = league,
            Score = 0,
            RoundsPlayed = 0,
            Rounds = [],
            BestRound = 0,
            AvgScore = 0
        });
        dbContext.Update(league);
        await dbContext.SaveChangesAsync();
        
        return Results.Ok();
    }
    
    private static async Task<IResult> CreateNewLeagueAsync(
        [FromBody] NewLeagueDto leagueDto,
        [FromServices] UserManager<AppUser> userManager, 
        [FromServices] AppIdentityDbContext  dbContext,
        [FromServices] IValidationService validationService,
        HttpContext context)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null) return Results.Unauthorized();
        
        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return Results.Unauthorized();

        var (isValid, errors) = validationService.ValidateModel(leagueDto);
        if (!isValid)
        {
            return Results.BadRequest(new { errors });
        }
        
        var league = new League
        {
            Name = leagueDto.Name,
            OwnerId = user.Id,
            Code = SecureCodeGenerator.GenerateCode(),
            Format = leagueDto.Format,
            TotalRounds = leagueDto.TotalRounds,
            RoundsToConsider = leagueDto.RoundsToConsider,
            MinimumRounds = leagueDto.MinimumRounds,
            TotalPrize = leagueDto.TotalPrize ?? 0,
            PrizePerPerson = leagueDto.PrizePerPerson,
            TotalPlayers = 0,
            PointsToGive = leagueDto.PointsToGive,
            IsActive = true,
        };
        
        await dbContext.AddAsync(league); 
        await dbContext.SaveChangesAsync();
        
        return Results.Ok(league);
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