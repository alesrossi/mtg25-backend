using System.Security.Claims;
using API.Dtos.Notifications;
using API.Dtos.Trades;
using API.Logging;
using API.Services;
using Core.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints.Trades;

public static partial class TradesEndpoints
{
    private static async Task<IResult> RequestTradeAsync(
        string userId,
        HttpContext context,
        [FromServices] UserManager<AppUser> userManager,
        [FromServices] NotificationService notificationService,
        [FromServices] ILogger<TradesEndpointLogCategory> logger)
    {
        const string operation = "Trades.Request";
        var requesterUserId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(requesterUserId))
        {
            logger.LogOperationWarning(operation, "Missing user id", new { userId });
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            logger.LogOperationWarning(operation, "Missing requested user id", new { requesterUserId });
            return Results.BadRequest(new { Error = "Requested user identifier is required." });
        }

        if (string.Equals(requesterUserId, userId, StringComparison.Ordinal))
        {
            logger.LogOperationWarning(operation, "Cannot request trade with self", new { requesterUserId });
            return Results.BadRequest(new { Error = "Cannot request a trade with yourself." });
        }

        var requester = await userManager.FindByIdAsync(requesterUserId);
        if (requester is null)
        {
            logger.LogOperationWarning(operation, "Requester user not found", new { requesterUserId });
            return Results.Unauthorized();
        }

        var requestedUser = await userManager.FindByIdAsync(userId);
        if (requestedUser is null)
        {
            logger.LogOperationWarning(operation, "Requested user not found", new { userId });
            return Results.NotFound(new { Error = "Requested user was not found." });
        }

        var notification = new NewNotificationDto
        {
            Name = "trade_request",
            Message = $"{requester.DisplayName} wants to trade with you.",
            Origin = $"trade_request.{requester.Id}",
            ObjectId = requester.Id,
            AppUserId = requestedUser.Id
        };

        await notificationService.CreateNotificationAsync(notification);
        logger.LogOperationSuccess(operation, new { requesterUserId, requestedUserId = requestedUser.Id });
        return Results.Accepted();
    }

    private static async Task<IResult> UpdateTradeAsync(
        string tradeId,
        [FromBody] UpdateTradeRequest request,
        HttpContext context,
        [FromServices] ITradeConnectionService tradeConnectionService,
        [FromServices] ILogger<TradesEndpointLogCategory> logger,
        CancellationToken cancellationToken)
    {
        const string operation = "Trades.Update";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
        {
            logger.LogOperationWarning(operation, "Missing user id", new { tradeId });
            return Results.Unauthorized();
        }

        try
        {
            var updated = await tradeConnectionService.UpdateConnectionAsync(tradeId, userId, request, cancellationToken);
            logger.LogOperationSuccess(operation, new { tradeId, userId });
            return Results.Ok(updated);
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
        catch (ArgumentException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { tradeId, userId });
            return Results.BadRequest(new { Error = ex.Message });
        }
    }

    private static async Task<IResult> CancelTradeAsync(
        string tradeId,
        HttpContext context,
        [FromServices] ITradeConnectionService tradeConnectionService,
        [FromServices] ILogger<TradesEndpointLogCategory> logger,
        CancellationToken cancellationToken)
    {
        const string operation = "Trades.Cancel";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
        {
            logger.LogOperationWarning(operation, "Missing user id", new { tradeId });
            return Results.Unauthorized();
        }

        try
        {
            await tradeConnectionService.CancelConnectionAsync(tradeId, userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { tradeId, userId });
            return Results.NoContent();
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

    private static async Task<IResult> CommitTradeAsync(
        string tradeId,
        HttpContext context,
        [FromServices] ITradeConnectionService tradeConnectionService,
        [FromServices] ILogger<TradesEndpointLogCategory> logger,
        CancellationToken cancellationToken)
    {
        const string operation = "Trades.Commit";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
        {
            logger.LogOperationWarning(operation, "Missing user id", new { tradeId });
            return Results.Unauthorized();
        }

        try
        {
            await tradeConnectionService.CommitTradeAsync(tradeId, userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { tradeId, userId });
            return Results.NoContent();
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
        catch (ArgumentException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { tradeId, userId });
            return Results.BadRequest(new { Error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { tradeId, userId });
            return Results.BadRequest(new { Error = ex.Message });
        }
    }
}
