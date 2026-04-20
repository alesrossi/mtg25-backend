using System.Security.Claims;
using API.Dtos.Teams;
using API.Helpers;
using API.Services;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints.Teams;

public static partial class TeamsEndpoints
{
    private static async Task<IResult> CreateTeamAsync(
        HttpContext context,
        [FromBody] CreateTeamDto dto,
        [FromServices] ITeamService teamService)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            var team = await teamService.CreateTeamAsync(dto, userId);
            return Results.Created($"/api/teams/{team.Id}", team);
        }
        catch (TeamServiceException ex)
        {
            return ProblemResultFactory.Create(context, ex.StatusCode, ex.Title, ex.Detail);
        }
    }

    private static async Task<IResult> UpdateTeamAsync(
        HttpContext context,
        int id,
        [FromBody] UpdateTeamDto dto,
        [FromServices] ITeamService teamService)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            var team = await teamService.UpdateTeamAsync(id, dto, userId);
            return Results.Ok(team);
        }
        catch (TeamServiceException ex)
        {
            return ProblemResultFactory.Create(context, ex.StatusCode, ex.Title, ex.Detail);
        }
    }

    private static async Task<IResult> DeleteTeamAsync(
        HttpContext context,
        int id,
        [FromServices] ITeamService teamService)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            await teamService.DeleteTeamAsync(id, userId);
            return Results.NoContent();
        }
        catch (TeamServiceException ex)
        {
            return ProblemResultFactory.Create(context, ex.StatusCode, ex.Title, ex.Detail);
        }
    }

    private static async Task<IResult> InviteMemberAsync(
        HttpContext context,
        int id,
        [FromBody] InviteMemberDto dto,
        [FromServices] ITeamService teamService)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            await teamService.InviteMemberAsync(id, userId, dto.TargetUserId);
            return Results.NoContent();
        }
        catch (TeamServiceException ex)
        {
            return ProblemResultFactory.Create(context, ex.StatusCode, ex.Title, ex.Detail);
        }
    }

    private static async Task<IResult> UpdateMemberRoleAsync(
        HttpContext context,
        int id,
        string userId,
        [FromBody] UpdateMemberRoleDto dto,
        [FromServices] ITeamService teamService)
    {
        var adminUserId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(adminUserId))
            return Results.Unauthorized();

        try
        {
            await teamService.UpdateMemberRoleAsync(id, adminUserId, userId, dto.NewRole);
            return Results.NoContent();
        }
        catch (TeamServiceException ex)
        {
            return ProblemResultFactory.Create(context, ex.StatusCode, ex.Title, ex.Detail);
        }
    }

    private static async Task<IResult> RemoveMemberAsync(
        HttpContext context,
        int id,
        string userId,
        [FromServices] ITeamService teamService)
    {
        var adminUserId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(adminUserId))
            return Results.Unauthorized();

        try
        {
            await teamService.RemoveMemberAsync(id, adminUserId, userId);
            return Results.NoContent();
        }
        catch (TeamServiceException ex)
        {
            return ProblemResultFactory.Create(context, ex.StatusCode, ex.Title, ex.Detail);
        }
    }

    private static async Task<IResult> AcceptInviteAsync(
        HttpContext context,
        int id,
        [FromServices] ITeamService teamService)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            await teamService.AcceptInviteAsync(id, userId);
            return Results.NoContent();
        }
        catch (TeamServiceException ex)
        {
            return ProblemResultFactory.Create(context, ex.StatusCode, ex.Title, ex.Detail);
        }
    }

    private static async Task<IResult> RejectInviteAsync(
        HttpContext context,
        int id,
        [FromServices] ITeamService teamService)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            await teamService.RejectInviteAsync(id, userId);
            return Results.NoContent();
        }
        catch (TeamServiceException ex)
        {
            return ProblemResultFactory.Create(context, ex.StatusCode, ex.Title, ex.Detail);
        }
    }

    private static async Task<IResult> CreateCollectionViewAsync(
        HttpContext context,
        int teamId,
        [FromBody] CreateTeamCollectionViewDto dto,
        [FromServices] ITeamCollectionService collectionService)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            var view = await collectionService.CreateCollectionViewAsync(teamId, dto, userId);
            return Results.Created($"/api/teams/collections/{view.Id}", view);
        }
        catch (TeamCollectionServiceException ex)
        {
            return ProblemResultFactory.Create(context, ex.StatusCode, ex.Title, ex.Detail);
        }
    }

    private static async Task<IResult> UpdateCollectionViewAsync(
        HttpContext context,
        int viewId,
        [FromBody] UpdateTeamCollectionViewDto dto,
        [FromServices] ITeamCollectionService collectionService)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            var view = await collectionService.UpdateCollectionViewAsync(viewId, dto, userId);
            return Results.Ok(view);
        }
        catch (TeamCollectionServiceException ex)
        {
            return ProblemResultFactory.Create(context, ex.StatusCode, ex.Title, ex.Detail);
        }
    }

    private static async Task<IResult> DeleteCollectionViewAsync(
        HttpContext context,
        int viewId,
        [FromServices] ITeamCollectionService collectionService)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            await collectionService.DeleteCollectionViewAsync(viewId, userId);
            return Results.NoContent();
        }
        catch (TeamCollectionServiceException ex)
        {
            return ProblemResultFactory.Create(context, ex.StatusCode, ex.Title, ex.Detail);
        }
    }
}
