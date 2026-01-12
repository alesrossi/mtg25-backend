using System.Security.Claims;
using API.Dtos.Friends;
using API.Extensions;
using API.Logging;
using API.Services;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints.Friends;

public static class FriendsEndpoints
{
    public static void MapFriendEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/friends")
            .WithTags("Friends")
            .WithProblemDetailsContract();

        group.MapGet("/", GetFriendsAsync)
            .RequireAuthorization()
            .WithSummary("List friends")
            .WithDescription("Returns the authenticated user's friends and pending requests.")
            .Produces<IReadOnlyList<FriendDto>>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json");

        group.MapPost("/{userId}/request", SendFriendRequestAsync)
            .RequireAuthorization()
            .WithSummary("Send friend request")
            .WithDescription("Creates or updates a pending friendship between two users.")
            .Produces(StatusCodes.Status202Accepted)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json");

        group.MapPost("/{userId}/accept", AcceptFriendRequestAsync)
            .RequireAuthorization()
            .WithSummary("Accept friend request")
            .WithDescription("Completes a pending friendship once the invited user approves it.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json");

        group.MapDelete("/{userId}", DeleteFriendAsync)
            .RequireAuthorization()
            .WithSummary("Delete friend")
            .WithDescription("Removes a friendship between two users.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json");
    }

    private static async Task<IResult> GetFriendsAsync(
        HttpContext context,
        [FromServices] IFriendService friendService,
        [FromServices] ILogger<FriendsEndpointLogCategory> logger,
        CancellationToken cancellationToken)
    {
        const string operation = "Friends.List";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
        {
            logger.LogOperationWarning(operation, "Missing user id");
            return Results.Unauthorized();
        }

        var friends = await friendService.GetFriendsAsync(userId, cancellationToken);
        logger.LogOperationSuccess(operation, new { userId, Count = friends.Count });
        return Results.Ok(friends);
    }

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
            return Results.Accepted();
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
}
