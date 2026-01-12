using System.Security.Claims;
using API.Logging;
using API.Services;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints.Friends;

public static partial class FriendsEndpoints
{
    private static async Task<IResult> SendFriendRequestAsync(
        string userId,
        HttpContext context,
        [FromServices] IFriendService friendService,
        [FromServices] ILogger<FriendsEndpointLogCategory> logger,
        CancellationToken cancellationToken)
    {
        const string operation = "Friends.Request";
        var requesterId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(requesterId))
        {
            logger.LogOperationWarning(operation, "Missing user id", new { userId });
            return Results.Unauthorized();
        }

        try
        {
            await friendService.SendFriendRequestAsync(requesterId, userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { requesterId, userId });
            return Results.Ok();
        }
        catch (ArgumentException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { requesterId, userId });
            return Results.BadRequest(new { Error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { requesterId, userId });
            return Results.BadRequest(new { Error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { requesterId, userId });
            return Results.NotFound(new { Error = ex.Message });
        }
    }

    private static async Task<IResult> AcceptFriendRequestAsync(
        string userId,
        HttpContext context,
        [FromServices] IFriendService friendService,
        [FromServices] ILogger<FriendsEndpointLogCategory> logger,
        CancellationToken cancellationToken)
    {
        const string operation = "Friends.Accept";
        var recipientId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(recipientId))
        {
            logger.LogOperationWarning(operation, "Missing user id", new { userId });
            return Results.Unauthorized();
        }

        if (string.Equals(userId, recipientId, StringComparison.Ordinal))
        {
            logger.LogOperationWarning(operation, "Requester cannot accept their own request", new { userId, recipientId });
            return Results.Forbid();
        }

        try
        {
            await friendService.AcceptFriendRequestAsync(userId, recipientId, cancellationToken);
            logger.LogOperationSuccess(operation, new { requesterId = userId, recipientId });
            return Results.Ok();
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { requesterId = userId, recipientId });
            return Results.Forbid();
        }
        catch (ArgumentException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { requesterId = userId, recipientId });
            return Results.BadRequest(new { Error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { requesterId = userId, recipientId });
            return Results.BadRequest(new { Error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { requesterId = userId, recipientId });
            return Results.NotFound(new { Error = ex.Message });
        }
    }

    private static async Task<IResult> DeleteFriendAsync(
        string userId,
        HttpContext context,
        [FromServices] IFriendService friendService,
        [FromServices] ILogger<FriendsEndpointLogCategory> logger,
        CancellationToken cancellationToken)
    {
        const string operation = "Friends.Delete";
        var requesterId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(requesterId))
        {
            logger.LogOperationWarning(operation, "Missing user id", new { userId });
            return Results.Unauthorized();
        }

        try
        {
            await friendService.DeleteFriendshipAsync(requesterId, userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { requesterId, userId });
            return Results.NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { requesterId, userId });
            return Results.NotFound(new { Error = ex.Message });
        }
    }

    private static async Task<IResult> RejectFriendRequestAsync(
        string userId,
        HttpContext context,
        [FromServices] IFriendService friendService,
        [FromServices] ILogger<FriendsEndpointLogCategory> logger,
        CancellationToken cancellationToken)
    {
        const string operation = "Friends.Reject";
        var recipientId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(recipientId))
        {
            logger.LogOperationWarning(operation, "Missing user id", new { userId });
            return Results.Unauthorized();
        }

        try
        {
            await friendService.RejectFriendRequestAsync(userId, recipientId, cancellationToken);
            logger.LogOperationSuccess(operation, new { requesterId = userId, recipientId });
            return Results.NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { requesterId = userId, recipientId });
            return Results.Forbid();
        }
        catch (ArgumentException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { requesterId = userId, recipientId });
            return Results.BadRequest(new { Error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { requesterId = userId, recipientId });
            return Results.BadRequest(new { Error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { requesterId = userId, recipientId });
            return Results.NotFound(new { Error = ex.Message });
        }
    }
}