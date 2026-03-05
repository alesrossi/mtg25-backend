using System.Security.Claims;
using API.Helpers;
using API.Logging;
using API.Services;
using Core.Models.Identity;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Endpoints.Trades;

public static partial class TradesEndpoints
{
    private static async Task<IResult> MatchUsersTradesAsync(
        [FromQuery] string initiatorUserId,
        [FromQuery] string partnerUserId,
        [FromServices] ITradeConnectionService tradeConnectionService,
        [FromServices] ILogger<TradesEndpointLogCategory> logger,
        [FromServices] UserManager<AppUser> userManager,
        [FromServices] MainContext identityDbContext,
        [FromServices] IMessageLocalizer messageLocalizer,
        HttpContext context,
        CancellationToken cancellationToken,
        [FromQuery] bool liveTrading = true)
    {
        const string operation = "Trades.Match";

        if (string.IsNullOrWhiteSpace(initiatorUserId) || string.IsNullOrWhiteSpace(partnerUserId))
        {
            logger.LogOperationWarning(operation, "Missing user identifiers", new { initiatorUserId, partnerUserId });
            return await LocalizedErrorResultFactory.BadRequestAsync(
                context,
                messageLocalizer,
                context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                "Errors.Trades.MissingUserIds");
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

        if (!liveTrading)
        {
            var hasApproval = await identityDbContext.Notifications
                .AsNoTracking()
                .AnyAsync(n =>
                    n.Name == "trade_request"
                    && n.Approval
                    && (
                        (n.AppUserId == initiatorUserId && n.ObjectId == partnerUserId)
                        || (n.AppUserId == partnerUserId && n.ObjectId == initiatorUserId)
                    ), cancellationToken);

            if (!hasApproval)
            {
                logger.LogOperationWarning(operation, "Trade request not approved", new { initiatorUserId, partnerUserId });
                return Results.Forbid();
            }
        }

        try
        {
            var connection = await tradeConnectionService.PrepareConnectionAsync(initiatorUserId, partnerUserId, liveTrading, cancellationToken);
            logger.LogOperationSuccess(operation, new { initiatorUserId, partnerUserId });
            return Results.Ok(connection);
        }
        catch (ArgumentException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { initiatorUserId, partnerUserId });
            return await LocalizedErrorResultFactory.BadRequestAsync(
                context,
                messageLocalizer,
                initiatorUserId,
                "Errors.Trades.InvalidRequest");
        }
        catch (KeyNotFoundException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { initiatorUserId, partnerUserId });
            return await LocalizedErrorResultFactory.NotFoundAsync(
                context,
                messageLocalizer,
                initiatorUserId,
                "Errors.Trades.NotFound");
        }
    }

    private static async Task<IResult> GetTradeConnectionAsync(
        string tradeId,
        HttpContext context,
        [FromServices] ITradeConnectionService tradeConnectionService,
        [FromServices] ILogger<TradesEndpointLogCategory> logger,
        [FromServices] IMessageLocalizer messageLocalizer,
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
            return await LocalizedErrorResultFactory.NotFoundAsync(
                context,
                messageLocalizer,
                userId,
                "Errors.Trades.NotFound");
        }
    }
}
