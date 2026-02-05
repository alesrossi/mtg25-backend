using System.Security.Claims;
using API.Endpoints.Leagues;
using API.Helpers;
using API.Logging;
using API.Services;
using Core.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints.Notifications;

public static partial class NotificationsEndpoints
{
    private static async Task<IResult> DeleteNotificationAsync(
        int id,
        HttpContext context,
        [FromServices] UserManager<AppUser> userManager,
        [FromServices] NotificationService notificationService,
        [FromServices] IMessageLocalizer messageLocalizer,
        [FromServices] ILogger<LeaguesEndpointLogCategory> logger)
    {
        const string operation = "Notifications.Delete";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id", new { id });
            return Results.Unauthorized();
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            logger.LogOperationWarning(operation, "User not found", new { userId });
            return Results.Unauthorized();
        }

        var successful = await notificationService.DeleteNotificationAsync(id);
        if (!successful)
        {
            logger.LogOperationWarning(operation, "Notification not found", new { id });
            return await LocalizedErrorResultFactory.NotFoundMessageAsync(
                context,
                messageLocalizer,
                userId,
                "Errors.Notifications.NotFound");
        }
        
        return Results.NoContent();
    }
    
    private static async Task<IResult> UpdateNotificationStatusAsync(
        HttpContext context,
        [FromBody] List<int> notificationIds,
        [FromServices] UserManager<AppUser> userManager,
        [FromServices] NotificationService notificationService,
        [FromServices] ILogger<LeaguesEndpointLogCategory> logger)
    {
        const string operation = "Notifications.UpdateStatus";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id", new { notificationIds.Count });
            return Results.Unauthorized();
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            logger.LogOperationWarning(operation, "User not found", new { userId });
            return Results.Unauthorized();
        }

        await notificationService.UpdateNotificationAsync(notificationIds, true, null);
        
        return Results.Ok();
    }

    private static async Task<IResult> ApproveNotificationAsync(
        HttpContext context,
        [FromBody] List<int> notificationIds,
        [FromServices] UserManager<AppUser> userManager,
        [FromServices] NotificationService notificationService,
        [FromServices] ILogger<LeaguesEndpointLogCategory> logger)
    {
        const string operation = "Notifications.UpdateApproval";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id", new { notificationIds.Count });
            return Results.Unauthorized();
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            logger.LogOperationWarning(operation, "User not found", new { userId });
            return Results.Unauthorized();
        }

        try
        {
            await notificationService.UpdateNotificationAsync(notificationIds, null, true, userId);
            return Results.Ok();
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
    }
}
