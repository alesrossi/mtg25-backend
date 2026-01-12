using API.Dtos.Trades;
using API.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints.Trades;

public partial class TradesEndpoints
{
    public static void MapTradeEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/trades")
            .WithTags("Trades")
            .WithProblemDetailsContract();

        MapTradesQueries(group);
        MapTradesCommands(group);
    }
    
    private static void MapTradesQueries(RouteGroupBuilder group)
    {
        group.MapGet("/match", MatchUsersTradesAsync)
            .RequireAuthorization()
            .WithSummary("Check if users have compatible items")
            .WithDescription("Checks if the two users have matching public wishlists and binders")
            .Produces<TradeConnectionDto>()
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapGet("/{tradeId}", GetTradeConnectionAsync)
            .RequireAuthorization()
            .WithSummary("Get trade session snapshot")
            .WithDescription("Returns the prepared trade connection so both participants can view wishlists")
            .Produces<TradeConnectionDto>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json");
    }
    
    private static void MapTradesCommands(RouteGroupBuilder group)
    {
        group.MapPost("/{tradeId}/request-commit", RequestTradeCommitAsync)
            .RequireAuthorization()
            .WithSummary("Request trade commitment")
            .WithDescription("Sends a notification asking the other participant to commit the trade session")
            .Produces(StatusCodes.Status202Accepted)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json");

        group.MapPost("/{userId}/request", RequestTradeAsync)
            .RequireAuthorization()
            .WithSummary("Request a trade with a user")
            .WithDescription("Validates both users exist and notifies the requested user about the trade request")
            .Produces(StatusCodes.Status202Accepted)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json");

        group.MapPut("/{tradeId}", UpdateTradeAsync)
            .RequireAuthorization()
            .WithSummary("Update current trade selection")
            .WithDescription("Allows both participants to adjust selected matches or their quantities within the current trade session")
            .Produces<TradeConnectionDto>()
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json");

        group.MapDelete("/{tradeId}", CancelTradeAsync)
            .RequireAuthorization()
            .WithSummary("Cancel trade session")
            .WithDescription("Allows any participant to stop and remove the current trade session")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json");

        group.MapPut("/{tradeId}/commit", CommitTradeAsync)
            .RequireAuthorization()
            .WithSummary("Commit trade session")
            .WithDescription("Finalizes the trade by transferring cards between collections, updating wishlists, and removing traded binder cards")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json");
    }
}
