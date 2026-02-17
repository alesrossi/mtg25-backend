using API.Dtos.Accounts;
using API.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints.Accounts;

public static partial class AccountsEndpoints
{
    public static void MapAccountEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/accounts")
            .WithTags("Accounts")
            .WithProblemDetailsContract();

        MapAccountQueries(group);
        MapAccountCommands(group);
    }
    
    private static void MapAccountQueries(RouteGroupBuilder group)
    {
        group.MapGet("/emailexists/{email}", CheckEmailExistsAsync)
            .WithSummary("Check if email exists")
            .WithDescription("Verifies if an email address is already registered in the system")
            .Produces<bool>();
        group.MapGet("/settings", GetSettingsAsync)
            .WithSummary("Check if email exists")
            .WithDescription("Verifies if an email address is already registered in the system")
            .Produces<bool>();
    }
    
    private static void MapAccountCommands(RouteGroupBuilder group)
    {
        group.MapPost("/register", RegisterUserAsync)
            .WithSummary("Register new user")
            .WithDescription("Creates a new user account with the provided registration details including name, email, and password")
            .Produces<UserDto>()
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapPost("/login", LoginUserAsync)
            .WithSummary("Authenticate user login")
            .WithDescription("Authenticates a user with email and password credentials, returning user information upon successful login")
            .Produces<AuthDto>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapGet("/logout", LogoutUserAsync)
            .RequireAuthorization()
            .WithSummary("Logout user")
            .WithDescription("Logs out the authenticated user by blacklisting their JWT token")
            .Produces<string>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapPost("/refresh", RefreshTokenAsync)
            .WithSummary("Refresh access token")
            .WithDescription("Issues a new access token using a valid refresh token cookie")
            .Produces<AuthDto>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapPut("/settings", UpdateSettingsAsync)
            .RequireAuthorization()
            .WithSummary("Update user settings")
            .WithDescription("Updates the authenticated user's settings")
            .Produces<SettingsForUserDto>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");
    }
}
