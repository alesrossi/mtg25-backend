using System.Security.Claims;
using API.Dtos.Leagues;
using API.Logging;
using API.Services;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints.Leagues;

public static partial class LeaguesEndpoint
{
    private static async Task<IResult> UpdateLeagueAsync(
        int id,
        [FromBody] UpdateLeagueDto updateLeague,
        HttpContext context,
        [FromServices] ILeagueService leagueService,
        [FromServices] ILogger<LeaguesEndpointLogCategory> logger,
        [FromServices] IMessageLocalizer messageLocalizer,
        CancellationToken cancellationToken)
    {
        const string operation = "Leagues.Update";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id", new { id });
            return Results.Unauthorized();
        }

        try
        {
            var league = await leagueService.UpdateLeagueAsync(id, userId, updateLeague, cancellationToken);
            logger.LogOperationSuccess(operation, new { id });
            return Results.Ok(league);
        }
        catch (LeagueServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { id, userId });
            return await MapLeagueServiceException(ex, context, messageLocalizer, userId);
        }
    }

    private static async Task<IResult> UpdateLeagueFromResultsAsync(
        int id,
        [FromBody] List<UserWithScore> userList,
        HttpContext context,
        [FromServices] ILeagueService leagueService,
        [FromServices] ILogger<LeaguesEndpointLogCategory> logger,
        [FromServices] IMessageLocalizer messageLocalizer,
        CancellationToken cancellationToken)
    {
        const string operation = "Leagues.UpdateResults";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id", new { id });
            return Results.Unauthorized();
        }

        try
        {
            await leagueService.UpdateLeagueResultsAsync(id, userId, userList, cancellationToken);
            logger.LogOperationSuccess(operation, new { id });
            return Results.Ok();
        }
        catch (LeagueServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { id, userId });
            return await MapLeagueServiceException(ex, context, messageLocalizer, userId);
        }
    }

    private static async Task<IResult> UpdateRoundAsync(
        int leagueId,
        int roundId,
        [FromBody] UpdateRoundDto updateRound,
        HttpContext context,
        [FromServices] ILeagueService leagueService,
        [FromServices] ILogger<LeaguesEndpointLogCategory> logger,
        [FromServices] IMessageLocalizer messageLocalizer,
        CancellationToken cancellationToken)
    {
        const string operation = "Leagues.UpdateRound";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id", new { leagueId, roundId });
            return Results.Unauthorized();
        }

        try
        {
            var round = await leagueService.UpdateRoundAsync(leagueId, roundId, userId, updateRound, cancellationToken);
            logger.LogOperationSuccess(operation, new { leagueId, roundId });
            var result = new RoundDto
            {
                Id = round.Id,
                Status = round.Status,
                StartDate = round.StartDate,
                Description = round.Description,
                Order = round.Order,
                LeagueId = round.LeagueId,
                Players = round.Players
                    .Select(player => new AppUserRoundDto
                    {
                        UserId = player.UserId,
                        Position = player.Position,
                        Score = player.Score,
                        Omw = player.Omw,
                        Gw = player.Gw,
                        Ogw = player.Ogw
                    })
                    .ToList()
            };

            return Results.Ok(result);
        }
        catch (LeagueServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { leagueId, roundId, userId });
            return await MapLeagueServiceException(ex, context, messageLocalizer, userId);
        }
    }

    private static async Task<IResult> GetInviteCodeAsync(
        int id,
        HttpContext context,
        [FromServices] ILeagueService leagueService,
        [FromServices] ILogger<LeaguesEndpointLogCategory> logger,
        [FromServices] IMessageLocalizer messageLocalizer,
        CancellationToken cancellationToken)
    {
        const string operation = "Leagues.InviteCode";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id", new { id });
            return Results.Unauthorized();
        }

        try
        {
            var inviteCode = await leagueService.GetInviteCodeAsync(id, userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { id });
            return Results.Ok(inviteCode);
        }
        catch (LeagueServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { id, userId });
            return await MapLeagueServiceException(ex, context, messageLocalizer, userId);
        }
    }

    private static async Task<IResult> RequestJoinLeagueFromCodeAsync(
        string code,
        HttpContext context,
        [FromServices] ILeagueService leagueService,
        [FromServices] ILogger<LeaguesEndpointLogCategory> logger,
        [FromServices] IMessageLocalizer messageLocalizer,
        CancellationToken cancellationToken)
    {
        const string operation = "Leagues.JoinByCode";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id", new { code });
            return Results.Unauthorized();
        }

        try
        {
            await leagueService.RequestJoinLeagueAsync(code, userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { code, userId });
            return Results.Ok();
        }
        catch (LeagueServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { code, userId });
            return await MapLeagueServiceException(ex, context, messageLocalizer, userId);
        }
    }
    
    private static async Task<IResult> JoinLeagueAsync(
        int id,
        HttpContext context,
        [FromServices] ILeagueService leagueService,
        [FromServices] ILogger<LeaguesEndpointLogCategory> logger,
        [FromServices] IMessageLocalizer messageLocalizer,
        CancellationToken cancellationToken)
    {
        const string operation = "Leagues.ApprovedUserJoin";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id", new { id });
            return Results.Unauthorized();
        }

        try
        {
            await leagueService.JoinLeagueAsync(id, userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { id, userId });
            return Results.Ok();
        }
        catch (LeagueServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { id, userId });
            return await MapLeagueServiceException(ex, context, messageLocalizer, userId);
        }
    }

    private static async Task<IResult> LeaveLeagueAsync(
        int id,
        HttpContext context,
        [FromServices] ILeagueService leagueService,
        [FromServices] ILogger<LeaguesEndpointLogCategory> logger,
        [FromServices] IMessageLocalizer messageLocalizer,
        CancellationToken cancellationToken)
    {
        const string operation = "Leagues.Leave";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id", new { id });
            return Results.Unauthorized();
        }

        try
        {
            await leagueService.LeaveLeagueAsync(id, userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { id, userId });
            return Results.Ok();
        }
        catch (LeagueServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { id, userId });
            return await MapLeagueServiceException(ex, context, messageLocalizer, userId);
        }
    }

    private static async Task<IResult> CreateNewLeagueAsync(
        [FromBody] NewLeagueDto leagueDto,
        HttpContext context,
        [FromServices] ILeagueService leagueService,
        [FromServices] ILogger<LeaguesEndpointLogCategory> logger,
        [FromServices] IMessageLocalizer messageLocalizer,
        CancellationToken cancellationToken)
    {
        const string operation = "Leagues.Create";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id");
            return Results.Unauthorized();
        }

        try
        {
            var league = await leagueService.CreateLeagueAsync(userId, leagueDto, cancellationToken);
            logger.LogOperationSuccess(operation, new { league.Id });
            return Results.Ok(league);
        }
        catch (LeagueServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { userId });
            return await MapLeagueServiceException(ex, context, messageLocalizer, userId);
        }
    }

    private static async Task<IResult> JoinAsPlayerAsync(
        int id,
        HttpContext context,
        [FromServices] ILeagueService leagueService,
        [FromServices] ILogger<LeaguesEndpointLogCategory> logger,
        [FromServices] IMessageLocalizer messageLocalizer,
        CancellationToken cancellationToken)
    {
        const string operation = "Leagues.JoinAsPlayer";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id", new { id });
            return Results.Unauthorized();
        }

        try
        {
            await leagueService.JoinAsPlayerAsync(id, userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { id, userId });
            return Results.Ok();
        }
        catch (LeagueServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { id, userId });
            return await MapLeagueServiceException(ex, context, messageLocalizer, userId);
        }
    }

    private static async Task<IResult> PromoteLeagueAdminAsync(
        int leagueId,
        string userId,
        HttpContext context,
        [FromServices] ILeagueService leagueService,
        [FromServices] ILogger<LeaguesEndpointLogCategory> logger,
        [FromServices] IMessageLocalizer messageLocalizer,
        CancellationToken cancellationToken)
    {
        const string operation = "Leagues.Promote";
        var callerId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (callerId is null)
        {
            logger.LogOperationWarning(operation, "Missing caller id", new { leagueId, targetUser = userId });
            return Results.Unauthorized();
        }

        try
        {
            await leagueService.PromoteLeagueAdminAsync(leagueId, callerId, userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { leagueId, targetUser = userId });
            return Results.Ok();
        }
        catch (LeagueServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { leagueId, callerId, targetUser = userId });
            return await MapLeagueServiceException(ex, context, messageLocalizer, userId);
        }
    }
}
