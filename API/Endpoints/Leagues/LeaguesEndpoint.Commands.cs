using System.Security.Claims;
using API.Dtos.Leagues;
using API.Dtos.Notifications;
using API.Logging;
using API.Services;
using Core.Enums;
using Core.Models.Identity;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Endpoints.Leagues;

public static partial class LeaguesEndpoint
{
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
        if (!await IsLeagueAdminAsync(dbContext, league, user.Id))
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
        if (updateLeague.IsPublic.HasValue) league.IsPublic = updateLeague.IsPublic.Value;
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
        if (!await IsLeagueAdminAsync(dbContext, league, userId))
        {
            logger.LogOperationWarning(operation, "Unauthorized", new { id, userId });
            return Results.Unauthorized();
        }

        var res = await dbContext.UserLeagues
            .Where(ul => ul.LeagueId == league.Id)
            .Include(ul => ul.User)
            .ToListAsync();
        
        var count = 0;
        foreach (var userWithScore in userList)
        {
            var userLeague = res.FirstOrDefault(x => x.UserId == userWithScore.UserId)!;
            if (league.ScoringSystem == ScoringSystem.Positional)
            {
                userLeague.Score += league.PointsToGive!.Count <= count ? 0 : league.PointsToGive![count];
                userLeague.BestRound = userLeague.BestRound == 0 || count + 1 < userLeague.BestRound ? count+1 : userLeague.BestRound;
                userLeague.RoundsPlayed += 1;
                userLeague.Rounds.Add(count+1);
                userLeague.AvgPosition = userLeague.Rounds.Average();
                league.TotalPrize += league.PrizePerPerson;
                
            }
            
            if (league.ScoringSystem == ScoringSystem.Victories)
            {
                userLeague.Score += (int)(userWithScore.Wins! * league.PointsPerWin! + userWithScore.Draws! * league.PointsPerDraw! + userWithScore.Losses! * league.PointsPerLoss!);
                userLeague.BestRound = userLeague.BestRound == 0 || count + 1 < userLeague.BestRound ? count+1 : userLeague.BestRound;
                userLeague.RoundsPlayed += 1;
                userLeague.Rounds.Add(count+1);
                userLeague.AvgPosition = userLeague.Rounds.Average();
                league.TotalPrize += league.PrizePerPerson;
            }
            count++;
        }
        if (league.CurrentRound + 1 <= league.TotalRounds) league.CurrentRound++;
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
        if (!await IsLeagueAdminAsync(dbContext, league, userId))
        {
            logger.LogOperationWarning(operation, "Unauthorized", new { id, userId });
            return Results.Unauthorized();
        }

        logger.LogOperationSuccess(operation, new { id });
        return Results.Ok(league.Code);
    }

    private static async Task<IResult> RequestJoinLeagueFromCodeAsync(
        string code,
        HttpContext context,
        [FromServices] UserManager<AppUser> userManager,
        [FromServices] AppIdentityDbContext dbContext,
        [FromServices] NotificationService notificationService,
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

        var adminAssignments = await dbContext.LeagueRoleAssignments
            .AsNoTracking()
            .Where(lr => lr.LeagueId == league.Id)
            .Select(lr => new { lr.UserId, lr.Roles })
            .ToListAsync();

        var adminRecipients = adminAssignments
            .Where(a => a.Roles.HasFlag(LeagueRole.Admin))
            .Select(a => a.UserId)
            .ToList();

        if (!adminRecipients.Contains(league.OwnerId))
        {
            adminRecipients.Add(league.OwnerId);
        }

        foreach (var adminId in adminRecipients.Distinct().Where(id => id != user.Id))
        {
            var newNotification = new NewNotificationDto
            {
                Name = "request_join_league",
                Message = $"User {user.FirstName} {user.LastName} wants to join {league.Name}",
                ObjectId = league.Id,
                Origin = league.Id + "." + user.Id,
                AppUserId = adminId
            };
        
            await notificationService.CreateNotificationAsync(newNotification);
        }

        logger.LogOperationSuccess(operation, new { code, userId });
        return Results.Ok();
    }
    
    private static async Task<IResult> JoinLeagueAsync(
        int id,
        HttpContext context,
        [FromServices] UserManager<AppUser> userManager,
        [FromServices] AppIdentityDbContext dbContext,
        [FromServices] NotificationService notificationService,
        [FromServices] ILogger<LeaguesEndpointLogCategory> logger)
    {
        const string operation = "Leagues.ApprovedUserJoin";
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

        var league = dbContext.Find<League>(id);
        if (league is null)
        {
            logger.LogOperationWarning(operation, "League not found", new { id });
            return Results.NotFound("League not found");
        }

        var existingMember = await dbContext.UserLeagues
            .AnyAsync(ul => ul.LeagueId == league.Id && ul.UserId == user.Id);
        if (existingMember)
        {
            logger.LogOperationWarning(operation, "Already member", new { id, userId });
            return Results.BadRequest("User already joined the league");
        }
        
        var exactMatch = $"{league.Id}.{userId}";
        
        var approved = await dbContext.Notifications
            .AnyAsync(n => 
                n.Origin == exactMatch
                && n.Approval);
        if (!approved)
        {
            logger.LogOperationWarning(operation, "User not approved", new { id, userId });
            return Results.BadRequest("User has not been approved by an admin");
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
            AvgPosition = 0
        });
        
        league.TotalPlayers++;
        dbContext.Update(league);
        await AssignLeagueRoleAsync(dbContext, user.Id, league.Id, LeagueRole.Player);
        await dbContext.SaveChangesAsync();
        
        var newNotification = new NewNotificationDto
        {
            Name = "player_joined_league",
            Message = $"{user.FirstName} {user.LastName} has joined {league.Name}",
            ObjectId = league.Id,
            Origin = $"{league.Id}.{user.Id}",
            AppUserId = league.OwnerId
        };
        
        await notificationService.CreateNotificationAsync(newNotification);
        
        logger.LogOperationSuccess(operation, new { league.Id, userId });
        return Results.Ok();
    }

    private static async Task<IResult> LeaveLeagueAsync(
        int id,
        HttpContext context,
        [FromServices] UserManager<AppUser> userManager,
        [FromServices] AppIdentityDbContext dbContext,
        [FromServices] NotificationService notificationService,
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

        var res = dbContext.UserLeagues.FirstOrDefault(ul => ul.LeagueId == league.Id && ul.UserId == userId);
        if (res is null)
        {
            logger.LogOperationWarning(operation, "User not in league", new { id, userId });
            return Results.BadRequest("User not playing in league");
        }

        res.IsPlaying = false;
        dbContext.Update(res);
        await RemoveLeagueRoleAsync(dbContext, userId, league.Id);
        await dbContext.SaveChangesAsync();
        
        var newNotification = new NewNotificationDto
        {
            Name = "user_leave_league",
            Message = $"User {user.FirstName} {user.LastName} left {league.Name}",
            ObjectId = league.Id,
            Origin = league.Id + "." + user.Id,
            AppUserId = league.OwnerId
        };
        
        await notificationService.CreateNotificationAsync(newNotification);

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
            PointsToGive = leagueDto.PointsToGive ?? null,
            IsActive = true,
            IsPublic = leagueDto.IsPublic,
            ScoringSystem = leagueDto.ScoringSystem,
            PointsPerWin = leagueDto.PointsPerWin ?? null,
            PointsPerDraw = leagueDto.PointsPerDraw ?? null,
            PointsPerLoss = leagueDto.PointsPerLoss ?? null
        };

        await dbContext.AddAsync(league);
        await dbContext.SaveChangesAsync();
        await AssignLeagueRoleAsync(dbContext, user.Id, league.Id, LeagueRole.Admin);
        await dbContext.SaveChangesAsync();

        var userLeague = new AppUserLeague
        {
            UserId = userId,
            LeagueId = league.Id,
            Score = 0,
            RoundsPlayed = 0,
            Rounds = [],
            BestRound = 0,
            AvgPosition = 0,
            IsPlaying = false
        };
        
        await dbContext.AddAsync(userLeague);
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
        var ownerMembership = await dbContext.UserLeagues
            .FirstOrDefaultAsync(ul => ul.LeagueId == league.Id && ul.UserId == user.Id);
        if (ownerMembership is null)
        {
            logger.LogOperationWarning(operation, "Owner membership missing", new { id, userId });
            return Results.BadRequest("Owner membership not found");
        }

        if (ownerMembership.IsPlaying)
        {
            logger.LogOperationWarning(operation, "Owner already playing", new { id, userId });
            return Results.BadRequest("Owner already joined as player");
        }

        ownerMembership.IsPlaying = true;
        dbContext.UserLeagues.Update(ownerMembership);

        league.TotalPlayers++;
        dbContext.Update(league);
        await AssignLeagueRoleAsync(dbContext, user.Id, league.Id, LeagueRole.Player);
        await dbContext.SaveChangesAsync();

        logger.LogOperationSuccess(operation, new { id, userId });
        return Results.Ok();
    }

    private static async Task<IResult> PromoteLeagueAdminAsync(
        int leagueId,
        string userId,
        HttpContext context,
        [FromServices] UserManager<AppUser> userManager,
        [FromServices] AppIdentityDbContext dbContext,
        [FromServices] ILogger<LeaguesEndpointLogCategory> logger)
    {
        const string operation = "Leagues.Promote";
        var callerId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (callerId is null)
        {
            logger.LogOperationWarning(operation, "Missing caller id", new { leagueId, targetUser = userId });
            return Results.Unauthorized();
        }

        var caller = await userManager.FindByIdAsync(callerId);
        if (caller is null)
        {
            logger.LogOperationWarning(operation, "Caller not found", new { callerId });
            return Results.Unauthorized();
        }

        var league = await dbContext.Leagues
            .AsTracking()
            .FirstOrDefaultAsync(l => l.Id == leagueId);
        if (league is null)
        {
            logger.LogOperationWarning(operation, "League not found", new { leagueId });
            return Results.NotFound();
        }

        if (league.OwnerId != caller.Id)
        {
            logger.LogOperationWarning(operation, "Caller not owner", new { leagueId, callerId });
            return Results.Unauthorized();
        }

        var targetUser = await userManager.FindByIdAsync(userId);
        if (targetUser is null)
        {
            logger.LogOperationWarning(operation, "Target user not found", new { userId });
            return Results.NotFound("User not found");
        }

        var isMember = await dbContext.UserLeagues
            .AsNoTracking()
            .AnyAsync(ul => ul.LeagueId == league.Id && ul.UserId == targetUser.Id && ul.IsPlaying);
        if (!isMember)
        {
            logger.LogOperationWarning(operation, "Target not part of league", new { leagueId, userId });
            return Results.BadRequest("User must be part of the league to be promoted");
        }

        await AssignLeagueRoleAsync(dbContext, targetUser.Id, league.Id, LeagueRole.Admin);
        await dbContext.SaveChangesAsync();

        logger.LogOperationSuccess(operation, new { leagueId, targetUser = targetUser.Id });
        return Results.Ok();
    }

    private static async Task AssignLeagueRoleAsync(
        AppIdentityDbContext dbContext,
        string userId,
        int leagueId,
        LeagueRole role)
    {
        var userExists = await dbContext.Users.AnyAsync(u => u.Id == userId);
        if (!userExists)
        {
            return;
        }

        var assignment = await dbContext.LeagueRoleAssignments
            .FirstOrDefaultAsync(x => x.LeagueId == leagueId && x.UserId == userId);

        if (assignment is null)
        {
            await dbContext.LeagueRoleAssignments.AddAsync(new LeagueRoleAssignment
            {
                LeagueId = leagueId,
                UserId = userId,
                Roles = role
            });
            return;
        }

        if (!assignment.Roles.HasFlag(role))
        {
            assignment.Roles |= role;
            dbContext.LeagueRoleAssignments.Update(assignment);
        }
    }

    private static async Task RemoveLeagueRoleAsync(
        AppIdentityDbContext dbContext,
        string userId,
        int leagueId)
    {
        var assignment = await dbContext.LeagueRoleAssignments
            .FirstOrDefaultAsync(x => x.LeagueId == leagueId && x.UserId == userId);

        if (assignment != null)
        {
            dbContext.LeagueRoleAssignments.Remove(assignment);
        }
    }

    private static async Task<bool> IsLeagueAdminAsync(
        AppIdentityDbContext dbContext,
        League league,
        string userId)
    {
        if (league.OwnerId == userId) return true;

        var assignment = await dbContext.LeagueRoleAssignments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.LeagueId == league.Id && x.UserId == userId);

        return assignment?.Roles.HasFlag(LeagueRole.Admin) == true;
    }
}
