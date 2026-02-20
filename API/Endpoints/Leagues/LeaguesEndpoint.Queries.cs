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
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

        try
        {
            var leagues = await leagueService.GetPublicLeaguesAsync(userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { leagues.Count, IsAnonymous = string.IsNullOrEmpty(userId) });
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
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

        try
        {
            var leagueDto = await leagueService.GetLeagueByIdAsync(id, userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { id, IsAnonymous = string.IsNullOrEmpty(userId) });
            return Results.Ok(leagueDto);
        }
        catch (LeagueServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { id, userId });
            return await MapLeagueServiceException(ex, context, messageLocalizer, userId);
        }
    }

    private static async Task<IResult> GetRoundByIdAsync(
        int leagueId,
        int roundId,
        HttpContext context,
        [FromServices] ILeagueService leagueService,
        [FromServices] ILogger<LeaguesEndpointLogCategory> logger,
        [FromServices] IMessageLocalizer messageLocalizer,
        CancellationToken cancellationToken)
    {
        const string operation = "Leagues.GetRound";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

        try
        {
            var round = await leagueService.GetRoundByIdAsync(leagueId, roundId, userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { leagueId, roundId, IsAnonymous = string.IsNullOrEmpty(userId) });
            return Results.Ok(round);
        }
        catch (LeagueServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { leagueId, roundId, userId });
            return await MapLeagueServiceException(ex, context, messageLocalizer, userId);
        }
    }

    private static async Task<IResult> GetRoundsByLeagueIdAsync(
        int leagueId,
        HttpContext context,
        [FromServices] ILeagueService leagueService,
        [FromServices] ILogger<LeaguesEndpointLogCategory> logger,
        [FromServices] IMessageLocalizer messageLocalizer,
        CancellationToken cancellationToken)
    {
        const string operation = "Leagues.GetRounds";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

        try
        {
            var rounds = await leagueService.GetRoundsByLeagueIdAsync(leagueId, userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { leagueId, rounds.Count, IsAnonymous = string.IsNullOrEmpty(userId) });
            return Results.Ok(rounds);
        }
        catch (LeagueServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { leagueId, userId });
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
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

        try
        {
            var leagueWithScores = await leagueService.GetLeagueScoresAsync(id, userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { id, userId, leagueWithScores.Scores.Count, IsAnonymous = string.IsNullOrEmpty(userId) });
            return Results.Ok(leagueWithScores);
        }
        catch (LeagueServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { id, userId });
            return await MapLeagueServiceException(ex, context, messageLocalizer, userId);
        }
    }
}
