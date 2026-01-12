using System.Security.Claims;
using API.Logging;
using API.Services;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints.Friends;

public static partial class FriendsEndpoints
{
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
        logger.LogOperationSuccess(operation, new { userId, friends.Count });
        return Results.Ok(friends);
    }
}