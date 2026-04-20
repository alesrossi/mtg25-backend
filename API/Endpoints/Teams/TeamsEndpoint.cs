using API.Extensions;
using API.Filters;

namespace API.Endpoints.Teams;

public static partial class TeamsEndpoints
{
    public static IEndpointRouteBuilder MapTeamsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/teams")
            .WithTags("Teams")
            .WithProblemDetailsContract()
            .RequireAuthorization();

        // Queries
        group.MapGet("/", GetTeamsAsync);
        group.MapGet("/{id:int}", GetTeamByIdAsync);
        group.MapGet("/{id:int}/members", GetTeamMembersAsync);
        group.MapGet("/{teamId:int}/collections", GetCollectionViewsAsync);
        group.MapGet("/collections/{viewId:int}", GetCollectionViewAsync);
        group.MapGet("/collections/{viewId:int}/cards", GetMergedCollectionCardsAsync);

        // Commands
        group.MapPost("/", CreateTeamAsync);
        group.MapPut("/{id:int}", UpdateTeamAsync);
        group.MapDelete("/{id:int}", DeleteTeamAsync);
        group.MapPost("/{id:int}/members", InviteMemberAsync);
        group.MapPut("/{id:int}/members/{userId}", UpdateMemberRoleAsync);
        group.MapDelete("/{id:int}/members/{userId}", RemoveMemberAsync);
        group.MapPost("/{id:int}/accept", AcceptInviteAsync);
        group.MapPost("/{id:int}/reject", RejectInviteAsync);
        group.MapPost("/{teamId:int}/collections", CreateCollectionViewAsync);
        group.MapPut("/collections/{viewId:int}", UpdateCollectionViewAsync);
        group.MapDelete("/collections/{viewId:int}", DeleteCollectionViewAsync);

        return app;
    }
}
