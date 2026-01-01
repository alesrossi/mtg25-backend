using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using API.Dtos.Trades;
using API.Logging;
using API.Services;
using Core.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints.Trades;

public static partial class TradesEndpoints
{
    private static async Task<IResult> MatchUsersTradesAsync(
        [FromQuery] string initiatorUserId,
        [FromQuery] string partnerUserId,
        [FromServices] ITradeConnectionService tradeConnectionService,
        [FromServices] ILogger<TradesEndpointLogCategory> logger,
        [FromServices] UserManager<AppUser> userManager,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        const string operation = "Trades.Match";

        if (string.IsNullOrWhiteSpace(initiatorUserId) || string.IsNullOrWhiteSpace(partnerUserId))
        {
            logger.LogOperationWarning(operation, "Missing user identifiers", new { initiatorUserId, partnerUserId });
            return Results.BadRequest(new { Error = "Both user identifiers are required." });
        }
        
        var jwtUserId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (jwtUserId is null || initiatorUserId !=  jwtUserId)
        {
            logger.LogOperationWarning(operation, "Missing user identifier");
            return Results.Unauthorized();
        }
        
        var firstUser = await userManager.FindByIdAsync(initiatorUserId);
        if (firstUser is null)
        {
            logger.LogOperationWarning(operation, "Initiator User not found", new { initiatorUserId });
            return Results.Unauthorized();
        }
        
        var secondUser = await userManager.FindByIdAsync(partnerUserId);
        if (secondUser is null)
        {
            logger.LogOperationWarning(operation, "Partner User not found", new { partnerUserId });
            return Results.Unauthorized();
        }

        try
        {
            var connection = await tradeConnectionService.PrepareConnectionAsync(initiatorUserId, partnerUserId, cancellationToken);
            logger.LogOperationSuccess(operation, new { initiatorUserId, partnerUserId });
            return Results.Ok(connection);
        }
        catch (ArgumentException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { initiatorUserId, partnerUserId });
            return Results.BadRequest(new { Error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { initiatorUserId, partnerUserId });
            return Results.NotFound(new { Error = ex.Message });
        }
    }

    private static async Task<IResult> GetTradeConnectionAsync(
        string tradeId,
        HttpContext context,
        [FromServices] ITradeConnectionService tradeConnectionService,
        [FromServices] ILogger<TradesEndpointLogCategory> logger,
        CancellationToken cancellationToken)
    {
        const string operation = "Trades.GetSession";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            logger.LogOperationWarning(operation, "Missing user id", new { tradeId });
            return Results.Unauthorized();
        }

        try
        {
            var connection = await tradeConnectionService.GetConnectionAsync(tradeId, userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { tradeId, userId });
            return Results.Ok(connection);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { tradeId, userId });
            return Results.Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { tradeId, userId });
            return Results.NotFound(new { Error = ex.Message });
        }
    }
}
