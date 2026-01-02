using System.Security.Claims;
using API.Dtos.Trades;
using API.Logging;
using API.Services;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints.Trades;

public static partial class TradesEndpoints
{
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

        if (request is null)
        {
            logger.LogOperationWarning(operation, "Missing payload", new { tradeId, userId });
            return Results.BadRequest(new { Error = "Update payload is required." });
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
