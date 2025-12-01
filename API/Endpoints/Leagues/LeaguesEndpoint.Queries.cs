using System.Security.Claims;
using API.Dtos.Leagues;
using API.Logging;
using API.Services;
using Core.Models.Identity;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Endpoints.Leagues;

public static partial class LeaguesEndpoint
{
    private static void MapLeagueQueries(RouteGroupBuilder group)
    {
        group.MapGet("/user", GetLeaguesFromUserAsync)
            .RequireAuthorization()
            .WithSummary("Get user's leagues")
            .WithDescription("Returns all leagues the authenticated user is participating in")
            .Produces<List<UserWithLeaguesDto>>()
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/", GetLeaguesAsync)
            .RequireAuthorization()
            .WithSummary("Get all leagues")
            .WithDescription("Returns all available leagues with pagination")
            .Produces<Helpers.Pagination<LeagueDto>>()
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/{id:int}", GetLeagueFromIdAsync)
            .RequireAuthorization()
            .WithSummary("Get league by ID")
            .WithDescription("Returns specific league details by ID")
            .Produces<LeagueDto>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{id:int}/invite", GetInviteCodeAsync)
            .RequireAuthorization()
            .WithSummary("Get league invite code")
            .WithDescription("Generates or retrieves invite code for league participation")
            .Produces<string>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{id:int}/scores", ListLeagueWithScores)
            .RequireAuthorization()
            .WithSummary("List Leagues and Users scores")
            .WithDescription("List leagues and user scores ranked from first to last")
            .Produces<LeagueWithScoresDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> GetLeaguesAsync(
        [FromServices] UserManager<AppUser> userManager,
        [FromServices] AppIdentityDbContext dbContext,
        HttpContext context,
        [FromServices] ILogger<LeaguesEndpointLogCategory> logger)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning("Leagues.QueryAll", "Missing user id");
            return Results.Unauthorized();
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            logger.LogOperationWarning("Leagues.QueryAll", "User not found", new { userId });
            return Results.Unauthorized();
        }

        var leagues = await dbContext.Leagues
            .AsNoTracking()
            .ToListAsync();
        logger.LogOperationSuccess("Leagues.QueryAll", new { Count = leagues.Count });
        return Results.Ok(leagues);
    }

    private static async Task<IResult> GetLeaguesFromUserAsync(
        [FromServices] UserManager<AppUser> userManager,
        [FromServices] AppIdentityDbContext dbContext,
        HttpContext context,
        [FromServices] ILogger<LeaguesEndpointLogCategory> logger)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning("Leagues.QueryUser", "Missing user id");
            return Results.Unauthorized();
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            logger.LogOperationWarning("Leagues.QueryUser", "User not found", new { userId });
            return Results.Unauthorized();
        }

        var res = await dbContext.UserLeagues
            .Where(ul => ul.UserId == userId)
            .Include(ul => ul.League)
            .AsNoTracking()
            .ToListAsync();

        var leaguesById = res
            .Select(x => new LeagueDto
            {
                Code = x.League.Code,
                Name = x.League.Name,
                Score = x.Score,
                Id = x.LeagueId,
                Format = x.League.Format,
                TotalRounds = x.League.TotalRounds,
                CurrentRound = x.League.CurrentRound,
                RoundsToConsider = x.League.RoundsToConsider,
                MinimumRounds = x.League.MinimumRounds,
                TotalPrize = x.League.TotalPrize,
                PrizePerPerson = x.League.PrizePerPerson,
                TotalPlayers = x.League.TotalPlayers,
                IsActive = x.League.IsActive,
                IsPlaying = x.IsPlaying,
                OwnerId = x.League.OwnerId
            })
            .ToDictionary(league => league.Id);

        var ownedLeagues = await dbContext.Leagues
            .Where(l => l.OwnerId == userId)
            .Select(league => new LeagueDto
            {
                Code = league.Code,
                Name = league.Name,
                Score = 0,
                Id = league.Id,
                Format = league.Format,
                TotalRounds = league.TotalRounds,
                CurrentRound = league.CurrentRound,
                RoundsToConsider = league.RoundsToConsider,
                MinimumRounds = league.MinimumRounds,
                TotalPrize = league.TotalPrize,
                PrizePerPerson = league.PrizePerPerson,
                TotalPlayers = league.TotalPlayers,
                IsActive = league.IsActive,
                IsPlaying = false,
                OwnerId = league.OwnerId
            })
            .AsNoTracking()
            .ToListAsync();

        foreach (var league in ownedLeagues)
        {
            leaguesById.TryAdd(league.Id, league);
        }

        var leaguesDto = leaguesById.Values.ToList();
        var dto = new UserWithLeaguesDto
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            DisplayName = user.DisplayName,
            Leagues = leaguesDto
        };
        logger.LogOperationSuccess("Leagues.QueryUser", new { userId, Count = leaguesDto.Count });
        return Results.Ok(dto);
    }

    private static async Task<IResult> GetLeagueFromIdAsync(
        int id,
        [FromServices] UserManager<AppUser> userManager,
        [FromServices] AppIdentityDbContext dbContext,
        HttpContext context,
        [FromServices] ILogger<LeaguesEndpointLogCategory> logger)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning("Leagues.GetById", "Missing user id", new { id });
            return Results.Unauthorized();
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            logger.LogOperationWarning("Leagues.GetById", "User not found", new { userId });
            return Results.Unauthorized();
        }

        var league = await dbContext.Leagues
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == id);
        if (league is null)
        {
            logger.LogOperationWarning("Leagues.GetById", "League not found", new { id });
            return Results.NotFound();
        }

        var res = await dbContext.UserLeagues
            .Where(ul => ul.LeagueId == league.Id && ul.UserId == userId)
            .Include(ul => ul.League)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (res is null)
        {
            logger.LogOperationWarning("Leagues.GetById", "User not in league", new { id, userId });
            return Results.Unauthorized();
        }

        var leagueDto = new LeagueDto
        {
            Id = league.Id,
            Name = league.Name,
            Code = league.Code,
            Format = league.Format,
            TotalRounds = league.TotalRounds,
            CurrentRound = league.CurrentRound,
            RoundsToConsider = league.RoundsToConsider,
            MinimumRounds = league.MinimumRounds,
            TotalPrize = league.TotalPrize,
            PrizePerPerson = league.PrizePerPerson,
            TotalPlayers = league.TotalPlayers,
            Score = res.Score,
            OwnerId = league.OwnerId,
            IsActive = league.IsActive,
            IsPlaying = res.IsPlaying
        };

        logger.LogOperationSuccess("Leagues.GetById", new { id });
        return Results.Ok(leagueDto);
    }

    private static async Task<IResult> ListLeagueWithScores(
        int id,
        [FromServices] UserManager<AppUser> userManager,
        [FromServices] AppIdentityDbContext dbContext,
        [FromServices] IValidationService validationService,
        HttpContext context)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null) return Results.Unauthorized();

        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return Results.Unauthorized();

        var league = await dbContext.Leagues
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == id);
        if (league is null) return Results.NotFound();

        var res = await dbContext.UserLeagues
            .Where(ul => ul.LeagueId == league.Id)
            .Include(ul => ul.User)
            .AsNoTracking()
            .ToListAsync();

        var leagueWithScores = new LeagueWithScoresDto
        {
            Id = league.Id,
            Name = league.Name,
            CurrentRound = league.CurrentRound,
            OwnerId = league.OwnerId
        };

        res.ForEach(x =>
        {
            if (x.LeagueId == league.Id && x.IsPlaying)
            {
                leagueWithScores.Scores.Add(new Score
                {
                    FirstName = x.User.FirstName,
                    LastName = x.User.LastName,
                    UserId = x.User.Id,
                    Points = x.Score,
                    RoundsPlayed = x.RoundsPlayed,
                    BestRound = x.BestRound,
                    AvgScore = x.AvgScore
                });
            }
        });

        return Results.Ok(leagueWithScores);
    }
}
