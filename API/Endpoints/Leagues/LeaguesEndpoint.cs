using API.Dtos.Leagues;
using API.Extensions;
using Core.Models.Identity;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints.Leagues;

public static partial class LeaguesEndpoint
{
    public static void MapLeaguesEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/leagues")
            .WithTags("Leagues")
            .WithProblemDetailsContract();

        MapLeagueQueries(group);
        MapLeagueCommands(group);
    }
    
    private static void MapLeagueQueries(RouteGroupBuilder group)
    {
        group.MapGet("/user", GetLeaguesFromUserAsync)
            .RequireAuthorization()
            .WithSummary("Get user's leagues")
            .WithDescription("Returns all leagues the authenticated user is participating in")
            .Produces<List<UserWithLeaguesDto>>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapGet("/", GetLeaguesAsync)
            .RequireAuthorization("OptionalAuth")
            .WithSummary("Get all leagues (public leagues accessible to all)")
            .WithDescription("Returns all available leagues with pagination")
            .Produces<Helpers.Pagination<LeagueDto>>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapGet("/{id:int}", GetLeagueFromIdAsync)
            .RequireAuthorization("OptionalAuth")
            .WithSummary("Get league by ID (public leagues accessible to all)")
            .WithDescription("Returns specific league details by ID")
            .Produces<LeagueDto>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapGet("/{id:int}/invite", GetInviteCodeAsync)
            .RequireAuthorization()
            .WithSummary("Get league invite code")
            .WithDescription("Generates or retrieves invite code for league participation. Only admins can call this route")
            .Produces<string>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapGet("/{id:int}/scores", ListLeagueWithScores)
            .RequireAuthorization("OptionalAuth")
            .WithSummary("List Leagues and Users scores (public leagues accessible to all)")
            .WithDescription("List leagues and user scores ranked from first to last")
            .Produces<LeagueWithScoresDto>()
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapGet("/{leagueId:int}/rounds/{roundId:int}", GetRoundByIdAsync)
            .RequireAuthorization("OptionalAuth")
            .WithSummary("Get round by id (public leagues accessible to all)")
            .WithDescription("Returns round information with ordered player results")
            .Produces<RoundInfoDto>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapGet("/{leagueId:int}/rounds", GetRoundsByLeagueIdAsync)
            .RequireAuthorization("OptionalAuth")
            .WithSummary("Get all rounds for a league (public leagues accessible to all)")
            .WithDescription("Returns all rounds information for a specific league, including virtual rounds not yet created")
            .Produces<IReadOnlyList<RoundInfoDto>>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapGet("/{id:int}/export", ExportLeagueToExcelAsync)
            .RequireAuthorization()
            .WithSummary("Export league to Excel")
            .WithDescription("Exports league standings with all rounds to an Excel file. Only admins can call this route")
            .Produces<FileResult>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");
    }
    
    private static void MapLeagueCommands(RouteGroupBuilder group)
    {
        group.MapPut("/{id:int}", UpdateLeagueAsync)
            .RequireAuthorization()
            .WithSummary("Update league")
            .WithDescription("Updates league information such as name, description, and settings. Only admins can call this route")
            .Produces<League>()
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapPost("/{id:int}/parse-results", ParseEventLinkPdfAsync)
            .RequireAuthorization()
            .WithSummary("Parse EventLink PDF")
            .WithDescription("Parses an EventLink tournament results PDF and returns a list of players with scores, matched to league members by companion name")
            .Produces<EventLinkParseResultDto>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json")
            .DisableAntiforgery();

        group.MapPatch("/{id:int}/results", UpdateLeagueFromResultsAsync)
            .RequireAuthorization()
            .WithSummary("Update league results")
            .WithDescription("Updates league standings and match results . Only admins can call this route")
            .Produces(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapPut("/{leagueId:int}/rounds/{roundId:int}", UpdateRoundAsync)
            .RequireAuthorization()
            .WithSummary("Update round")
            .WithDescription("Updates round details and players for a league. Only admins can call this route")
            .Produces<RoundInfoDto>()
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapPatch("/{code}/request", RequestJoinLeagueFromCodeAsync)
            .RequireAuthorization()
            .WithSummary("Request to join league")
            .WithDescription("Request to join authenticated user to league using invite code")
            .Produces(StatusCodes.Status200OK)
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");
        
        group.MapPatch("/{id:int}/join/{userId}", JoinLeagueAsync)
            .RequireAuthorization()
            .WithSummary("Admit User")
            .WithDescription("Admin admits user into league, requires previous approval")
            .Produces(StatusCodes.Status200OK)
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapPatch("/{id:int}/leave", LeaveLeagueAsync)
            .RequireAuthorization()
            .WithSummary("Leave league by id")
            .WithDescription("User leaves league given id")
            .Produces(StatusCodes.Status200OK)
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapPost("/", CreateNewLeagueAsync)
            .RequireAuthorization()
            .WithSummary("Create new league")
            .WithDescription("Creates new league with specified settings and options")
            .Produces<League>()
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapPatch("/{id:int}/owner-join", JoinAsPlayerAsync)
            .RequireAuthorization()
            .WithSummary("Owner of league joins as player")
            .WithDescription("Owner of league joins as player")
            .Produces(StatusCodes.Status200OK)
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapPatch("/{leagueId:int}/promote/{userId}", PromoteLeagueAdminAsync)
            .RequireAuthorization()
            .WithSummary("Promote player to admin")
            .WithDescription("Allows the league owner to grant admin role to a player")
            .Produces(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");
        
        group.MapPatch("/{leagueId:int}/terminate", TerminateLeagueAsync)
            .RequireAuthorization()
            .WithSummary("Terminates league")
            .WithDescription("Terminates an active league setting the flag to false")
            .Produces(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapPatch("/{leagueId:int}/rounds/{roundId:int}/join", JoinRoundAsync)
            .RequireAuthorization()
            .WithSummary("Join round")
            .WithDescription("Authenticated league member joins a round that is in Playing state")
            .Produces(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapPatch("/{leagueId:int}/rounds/{roundId:int}/leave", LeaveRoundAsync)
            .RequireAuthorization()
            .WithSummary("Leave round")
            .WithDescription("Authenticated participant leaves a round that is in Playing state")
            .Produces(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");
    }
}
