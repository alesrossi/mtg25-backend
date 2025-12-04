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
    private static void MapLeagueCommands(RouteGroupBuilder group)
    {
        group.MapPut("/{id:int}", UpdateLeagueAsync)
            .RequireAuthorization()
            .WithSummary("Update league")
            .WithDescription("Updates league information such as name, description, and settings")
            .Produces<League>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPatch("/{id:int}/results", UpdateLeagueFromResultsAsync)
            .RequireAuthorization()
            .WithSummary("Update league results")
            .WithDescription("Updates league standings and match results")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPatch("/{code}/join", JoinLeagueFromCodeAsync)
            .RequireAuthorization()
            .WithSummary("Join league by code")
            .WithDescription("Joins authenticated user to league using invite code")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPatch("/{id:int}/leave", LeaveLeagueAsync)
            .RequireAuthorization()
            .WithSummary("Leave league by id")
            .WithDescription("User leaves league given id")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/", CreateNewLeagueAsync)
            .RequireAuthorization()
            .WithSummary("Create new league")
            .WithDescription("Creates new league with specified settings and options")
            .Produces<League>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapPatch("/{id:int}/join", JoinAsPlayerAsync)
            .RequireAuthorization()
            .WithSummary("Owner of league joins as player")
            .WithDescription("Owner of league joins as player")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> UpdateLeagueAsync(
        int id,
        [FromServices] UserManager<AppUser> userManager,
        [FromServices] AppIdentityDbContext dbContext,
        [FromServices] IValidationService validationService,
        [FromBody] UpdateLeagueDto updateLeague,
        HttpContext context,
        [FromServices] ILogger<LeaguesEndpointLogCategory> logger)
    {
        const string operation = "Leagues.Update";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id", new { id });
            return Results.Unauthorized();
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            logger.LogOperationWarning(operation, "User not found", new { userId });
            return Results.Unauthorized();
        }

        var league = await dbContext.Leagues
            .AsTracking()
            .FirstOrDefaultAsync(l => l.Id == id);
        if (league is null)
        {
            logger.LogOperationWarning(operation, "League not found", new { id });
            return Results.NotFound();
        }
        if (league.OwnerId != user.Id)
        {
            logger.LogOperationWarning(operation, "Unauthorized", new { id, userId });
            return Results.Unauthorized();
        }

        var (isValid, errors) = validationService.ValidateModel(updateLeague);
        if (!isValid)
        {
            logger.LogOperationWarning(operation, "Validation failed", new { id, errors });
            return Results.BadRequest(new { errors });
        }

        if (updateLeague.Name != null) league.Name = updateLeague.Name;
        if (updateLeague.TotalRounds != null) league.TotalRounds = (int)updateLeague.TotalRounds;
        if (updateLeague.RoundsToConsider != null) league.RoundsToConsider = (int)updateLeague.RoundsToConsider;
        if (updateLeague.MinimumRounds != null) league.MinimumRounds = (int)updateLeague.MinimumRounds;
        if (updateLeague.TotalPrize != null) league.TotalPrize = (double)updateLeague.TotalPrize;
        if (updateLeague.PrizePerPerson != null) league.PrizePerPerson = (double)updateLeague.PrizePerPerson;
        if (updateLeague.CurrentRound == null || updateLeague.CurrentRound > league.TotalRounds) return Results.BadRequest();
        league.CurrentRound = (int)updateLeague.CurrentRound;
             
        dbContext.Update(league);
        await dbContext.SaveChangesAsync();

        logger.LogOperationSuccess(operation, new { id });
        return Results.Ok(league);
    }

    private static async Task<IResult> UpdateLeagueFromResultsAsync(
        int id,
        [FromBody] List<UserWithScore> userList,
        [FromServices] UserManager<AppUser> userManager,
        [FromServices] AppIdentityDbContext dbContext,
        HttpContext context,
        [FromServices] ILogger<LeaguesEndpointLogCategory> logger)
    {
        const string operation = "Leagues.UpdateResults";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id", new { id });
            return Results.Unauthorized();
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            logger.LogOperationWarning(operation, "User not found", new { userId });
            return Results.Unauthorized();
        }

        var league = await dbContext.Leagues
            .AsTracking()
            .FirstOrDefaultAsync(l => l.Id == id);
        if (league is null)
        {
            logger.LogOperationWarning(operation, "League not found", new { id });
            return Results.NotFound();
        }
        if (league.OwnerId != userId)
        {
            logger.LogOperationWarning(operation, "Unauthorized", new { id, userId });
            return Results.Unauthorized();
        }

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
                if (league.CurrentRound + 1 <= league.TotalRounds) league.CurrentRound++;
            }
        });

        dbContext.Update(league);
        dbContext.UpdateRange(res);
        await dbContext.SaveChangesAsync();

        logger.LogOperationSuccess(operation, new { id });
        return Results.Ok();
    }

    private static async Task<IResult> GetInviteCodeAsync(
        int id,
        [FromServices] UserManager<AppUser> userManager,
        [FromServices] AppIdentityDbContext dbContext,
        HttpContext context,
        [FromServices] ILogger<LeaguesEndpointLogCategory> logger)
    {
        const string operation = "Leagues.InviteCode";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id", new { id });
            return Results.Unauthorized();
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            logger.LogOperationWarning(operation, "User not found", new { userId });
            return Results.Unauthorized();
        }

        var league = await dbContext.Leagues
            .AsTracking()
            .FirstOrDefaultAsync(l => l.Id == id);
        if (league is null)
        {
            logger.LogOperationWarning(operation, "League not found", new { id });
            return Results.NotFound();
        }
        if (league.OwnerId != userId)
        {
            logger.LogOperationWarning(operation, "Unauthorized", new { id, userId });
            return Results.Unauthorized();
        }

        logger.LogOperationSuccess(operation, new { id });
        return Results.Ok(league.Code);
    }

    private static async Task<IResult> JoinLeagueFromCodeAsync(
        string code,
        [FromServices] UserManager<AppUser> userManager,
        [FromServices] AppIdentityDbContext dbContext,
        HttpContext context,
        [FromServices] ILogger<LeaguesEndpointLogCategory> logger)
    {
        const string operation = "Leagues.JoinByCode";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id", new { code });
            return Results.Unauthorized();
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            logger.LogOperationWarning(operation, "User not found", new { userId });
            return Results.Unauthorized();
        }

        var league = await dbContext.Leagues
            .FirstOrDefaultAsync(l => l.Code == code);
        if (league is null)
        {
            logger.LogOperationWarning(operation, "League not found", new { code });
            return Results.NotFound("League not found");
        }

        var existingMember = await dbContext.UserLeagues
            .AnyAsync(ul => ul.LeagueId == league.Id && ul.UserId == user.Id);
        if (existingMember)
        {
            logger.LogOperationWarning(operation, "Already member", new { code, userId });
            return Results.BadRequest("User already joined the league");
        }

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

        league.TotalPlayers++;
        dbContext.Update(league);
        await dbContext.SaveChangesAsync();

        logger.LogOperationSuccess(operation, new { code, userId });
        return Results.Ok();
    }

    private static async Task<IResult> LeaveLeagueAsync(
        int id,
        [FromServices] UserManager<AppUser> userManager,
        [FromServices] AppIdentityDbContext dbContext,
        HttpContext context,
        [FromServices] ILogger<LeaguesEndpointLogCategory> logger)
    {
        const string operation = "Leagues.Leave";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id", new { id });
            return Results.Unauthorized();
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            logger.LogOperationWarning(operation, "User not found", new { userId });
            return Results.Unauthorized();
        }

        var league = await dbContext.Leagues
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == id);
        if (league is null)
        {
            logger.LogOperationWarning(operation, "League not found", new { id });
            return Results.NotFound("League not found");
        }
        if (league.OwnerId == user.Id)
        {
            logger.LogOperationWarning(operation, "Owner leave attempt", new { id, userId });
            return Results.BadRequest("You can't leave a league you created");
        }

        var res = dbContext.UserLeagues
            .Where(ul => ul.LeagueId == league.Id && ul.UserId == userId).FirstOrDefault();
        if (res is null)
        {
            logger.LogOperationWarning(operation, "User not in league", new { id, userId });
            return Results.BadRequest("User not playing in league");
        }

        res.IsPlaying = false;
        dbContext.Update(res);
        await dbContext.SaveChangesAsync();

        logger.LogOperationSuccess(operation, new { id, userId });
        return Results.Ok();
    }

    private static async Task<IResult> CreateNewLeagueAsync(
        [FromBody] NewLeagueDto leagueDto,
        [FromServices] UserManager<AppUser> userManager,
        [FromServices] AppIdentityDbContext dbContext,
        [FromServices] IValidationService validationService,
        HttpContext context,
        [FromServices] ILogger<LeaguesEndpointLogCategory> logger)
    {
        const string operation = "Leagues.Create";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id");
            return Results.Unauthorized();
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            logger.LogOperationWarning(operation, "User not found", new { userId });
            return Results.Unauthorized();
        }

        var (isValid, errors) = validationService.ValidateModel(leagueDto);
        if (!isValid)
        {
            logger.LogOperationWarning(operation, "Validation failed", new { errors });
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

        logger.LogOperationSuccess(operation, new { league.Id });
        return Results.Ok(league);
    }

    private static async Task<IResult> JoinAsPlayerAsync(
        int id,
        [FromServices] UserManager<AppUser> userManager,
        [FromServices] AppIdentityDbContext dbContext,
        HttpContext context,
        [FromServices] ILogger<LeaguesEndpointLogCategory> logger)
    {
        const string operation = "Leagues.JoinAsPlayer";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id", new { id });
            return Results.Unauthorized();
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            logger.LogOperationWarning(operation, "User not found", new { userId });
            return Results.Unauthorized();
        }

        var league = await dbContext.Set<League>().Where(x => x.Id == id).FirstOrDefaultAsync();
        if (league is null)
        {
            logger.LogOperationWarning(operation, "League not found", new { id });
            return Results.NotFound("League not found");
        }
        if (league.OwnerId != userId)
        {
            logger.LogOperationWarning(operation, "Unauthorized", new { id, userId });
            return Results.Unauthorized();
        }
        if (!league.IsActive)
        {
            logger.LogOperationWarning(operation, "League inactive", new { id });
            return Results.BadRequest();
        }
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

        logger.LogOperationSuccess(operation, new { id, userId });
        return Results.Ok();
    }
}
