using System.Security.Claims;
using API.Helpers;
using API.Services;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints.Teams;

public static partial class TeamsEndpoints
{
    private static async Task<IResult> GetTeamsAsync(
        HttpContext context,
        [FromServices] ITeamService teamService)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            var teams = await teamService.GetTeamsForUserAsync(userId);
            return Results.Ok(teams);
        }
        catch (TeamServiceException ex)
        {
            return ProblemResultFactory.Create(context, ex.StatusCode, ex.Title, ex.Detail);
        }
    }

    private static async Task<IResult> GetTeamByIdAsync(
        HttpContext context,
        int id,
        [FromServices] ITeamService teamService)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            var team = await teamService.GetTeamByIdAsync(id, userId);
            return Results.Ok(team);
        }
        catch (TeamServiceException ex)
        {
            return ProblemResultFactory.Create(context, ex.StatusCode, ex.Title, ex.Detail);
        }
    }

    private static async Task<IResult> GetTeamMembersAsync(
        HttpContext context,
        int id,
        [FromServices] ITeamService teamService)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            var members = await teamService.GetTeamMembersAsync(id, userId);
            return Results.Ok(members);
        }
        catch (TeamServiceException ex)
        {
            return ProblemResultFactory.Create(context, ex.StatusCode, ex.Title, ex.Detail);
        }
    }

    private static async Task<IResult> GetCollectionViewsAsync(
        HttpContext context,
        int teamId,
        [FromServices] ITeamCollectionService collectionService)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            var views = await collectionService.GetCollectionViewsAsync(teamId, userId);
            return Results.Ok(views);
        }
        catch (TeamCollectionServiceException ex)
        {
            return ProblemResultFactory.Create(context, ex.StatusCode, ex.Title, ex.Detail);
        }
    }

    private static async Task<IResult> GetCollectionViewAsync(
        HttpContext context,
        int viewId,
        [FromServices] ITeamCollectionService collectionService)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            var view = await collectionService.GetCollectionViewAsync(viewId, userId);
            return Results.Ok(view);
        }
        catch (TeamCollectionServiceException ex)
        {
            return ProblemResultFactory.Create(context, ex.StatusCode, ex.Title, ex.Detail);
        }
    }

    private static async Task<IResult> GetMergedCollectionCardsAsync(
        HttpContext context,
        int viewId,
        [FromServices] ITeamCollectionService collectionService)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            var cards = await collectionService.GetMergedCollectionCardsAsync(viewId, userId);
            return Results.Ok(cards);
        }
        catch (TeamCollectionServiceException ex)
        {
            return ProblemResultFactory.Create(context, ex.StatusCode, ex.Title, ex.Detail);
        }
    }
}
