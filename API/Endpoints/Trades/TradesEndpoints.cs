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
        // MapTradesCommands(group);
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
    
    // private static void MapTradesCommands(RouteGroupBuilder group)
    // {
    //     group.MapPost("/register", RegisterUserAsync)
    //         .WithSummary("Register new user")
    //         .WithDescription("Creates a new user account with the provided registration details including name, email, and password")
    //         .Produces<UserDto>()
    //         .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
    //         .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");
    //
    //     group.MapPost("/login", LoginUserAsync)
    //         .WithSummary("Authenticate user login")
    //         .WithDescription("Authenticates a user with email and password credentials, returning user information upon successful login")
    //         .Produces<AuthDto>()
    //         .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
    //         .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");
    //
    //     group.MapGet("/logout", LogoutUserAsync)
    //         .RequireAuthorization()
    //         .WithSummary("Logout user")
    //         .WithDescription("Logs out the authenticated user by blacklisting their JWT token")
    //         .Produces<string>()
    //         .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
    //         .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");
    //
    //     group.MapPut("/settings", UpdateSettingsAsync)
    //         .RequireAuthorization()
    //         .WithSummary("Update user settings")
    //         .WithDescription("Updates the authenticated user's settings")
    //         .Produces<SettingsForUserDto>()
    //         .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
    //         .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
    //         .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");
    // }
}
