using System.Security.Claims;
using API.Helpers;
using API.Logging;
using API.Services;
using Core.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints.Notifications;

public static partial class NotificationsEndpoints
{
    private static async Task<IResult> GetNotificationFromIdAsync(
        int id,
        HttpContext context,
        [FromServices] UserManager<AppUser> userManager,
        [FromServices] NotificationService notificationService,
        [FromServices] IMessageLocalizer messageLocalizer,
        [FromServices] ILogger<NotificationsEndpointsLogCategory> logger)
    {
        const string operation = "Notifications.GetById";
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

        var notification = await notificationService.GetNotificationAsync(id, userId);
        if (notification is null)
        {
            logger.LogOperationWarning(operation, "Notification not found", new { id, userId });
            return await LocalizedErrorResultFactory.NotFoundMessageAsync(
                context,
                messageLocalizer,
                userId,
                "Errors.Notifications.NotFound");
        }

        logger.LogOperationSuccess(operation, new { id, userId });
        return Results.Ok(notification);
    }
    
    private static async Task<IResult> GetAllUserNotificationsAsync(
        HttpContext context,
        [FromServices] UserManager<AppUser> userManager,
        [FromServices] NotificationService notificationService,
        [FromServices] ILogger<NotificationsEndpointsLogCategory> logger)
    {
        const string operation = "Notifications.GetAll";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id");
            return Results.Unauthorized();
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            logger.LogOperationWarning(operation, "User not found", new { userId });
            return Results.Unauthorized();
        }

        var notifications = await notificationService.GetUserNotificationsAsync(userId);
        logger.LogOperationSuccess(operation, new { userId, notifications.Count });
        return Results.Ok(notifications);
    }
}
