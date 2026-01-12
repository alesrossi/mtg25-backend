using System.Security.Claims;
using API.Dtos.Friends;
using API.Extensions;
using API.Logging;
using API.Services;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints.Friends;

public static partial class FriendsEndpoints
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
            .Produces(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json");

        group.MapPost("/{userId}/accept", AcceptFriendRequestAsync)
            .RequireAuthorization()
            .WithSummary("Accept friend request")
            .WithDescription("Completes a pending friendship once the invited user approves it.")
            .Produces(StatusCodes.Status200OK)
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

        group.MapPost("/{userId}/reject", RejectFriendRequestAsync)
            .RequireAuthorization()
            .WithSummary("Reject friend request")
            .WithDescription("Allows the invited user to decline a pending friend request.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json");
    }
}
