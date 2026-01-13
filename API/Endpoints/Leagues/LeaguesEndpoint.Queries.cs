using System.Security.Claims;
using API.Logging;
using API.Services;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints.Leagues;

public static partial class LeaguesEndpoint
{
    private static async Task<IResult> GetLeaguesAsync(
        HttpContext context,
        [FromServices] ILeagueService leagueService,
        [FromServices] ILogger<LeaguesEndpointLogCategory> logger,
        [FromServices] IMessageLocalizer messageLocalizer,
        CancellationToken cancellationToken)
    {
        const string operation = "Leagues.QueryAll";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id");
            return Results.Unauthorized();
        }

        try
        {
            var leagues = await leagueService.GetPublicLeaguesAsync(userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { leagues.Count });
            return Results.Ok(leagues);
        }
        catch (LeagueServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { userId });
            return await MapLeagueServiceException(ex, context, messageLocalizer, userId);
        }
    }

    private static async Task<IResult> GetLeaguesFromUserAsync(
        HttpContext context,
        [FromServices] ILeagueService leagueService,
        [FromServices] ILogger<LeaguesEndpointLogCategory> logger,
        [FromServices] IMessageLocalizer messageLocalizer,
        CancellationToken cancellationToken)
    {
        const string operation = "Leagues.QueryUser";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id");
            return Results.Unauthorized();
        }

        try
        {
            var dto = await leagueService.GetLeaguesForUserAsync(userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { userId, dto.Leagues.Count });
            return Results.Ok(dto);
        }
        catch (LeagueServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { userId });
            return await MapLeagueServiceException(ex, context, messageLocalizer, userId);
        }
    }

    private static async Task<IResult> GetLeagueFromIdAsync(
        int id,
        HttpContext context,
        [FromServices] ILeagueService leagueService,
        [FromServices] ILogger<LeaguesEndpointLogCategory> logger,
        [FromServices] IMessageLocalizer messageLocalizer,
        CancellationToken cancellationToken)
    {
        const string operation = "Leagues.GetById";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id", new { id });
            return Results.Unauthorized();
        }

        try
        {
            var leagueDto = await leagueService.GetLeagueByIdAsync(id, userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { id });
            return Results.Ok(leagueDto);
        }
        catch (LeagueServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { id, userId });
            return await MapLeagueServiceException(ex, context, messageLocalizer, userId);
        }
    }

    private static async Task<IResult> ListLeagueWithScores(
        int id,
        HttpContext context,
        [FromServices] ILeagueService leagueService,
        [FromServices] ILogger<LeaguesEndpointLogCategory> logger,
        [FromServices] IMessageLocalizer messageLocalizer,
        CancellationToken cancellationToken)
    {
        const string operation = "Leagues.ListScores";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null) return Results.Unauthorized();

        try
        {
            var leagueWithScores = await leagueService.GetLeagueScoresAsync(id, userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { id, userId, leagueWithScores.Scores.Count });
            return Results.Ok(leagueWithScores);
        }
        catch (LeagueServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { id, userId });
            return await MapLeagueServiceException(ex, context, messageLocalizer, userId);
        }
    }
}
