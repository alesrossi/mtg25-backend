using System.Security.Claims;
using API.Dtos.Notifications;
using API.Logging;
using API.Services;
using Core.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace API.Endpoints.Notifications;

public static partial class NotificationsEndpoints
{
    private static void MapNotificationsQueries(RouteGroupBuilder group)
    {
        group.MapGet("/{id:int}", GetNotificationFromIdAsync)
            .RequireAuthorization()
            .WithSummary("Retrieves notification")
            .WithDescription("Retrieves notification from given id")
            .Produces<NotificationDto>()
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json");
        
        group.MapGet("/", GetAllUserNotificationsAsync)
            .RequireAuthorization()
            .WithSummary("Retrieves all notifications for logged in user")
            .WithDescription("Retrieves all notifications for logged in user")
            .Produces<IReadOnlyList<NotificationDto>>()
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json");
        
    }
    
    private static async Task<IResult> GetNotificationFromIdAsync(
        int id,
        HttpContext context,
        [FromServices] UserManager<AppUser> userManager,
        [FromServices] NotificationService notificationService,
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
            return Results.NotFound("Notification not found");
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
            logger.LogOperationWarning(operation, "Missing user id", null);
            return Results.Unauthorized();
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            logger.LogOperationWarning(operation, "User not found", new { userId });
            return Results.Unauthorized();
        }

        var notifications = await notificationService.GetUserNotificationsAsync(userId);
        logger.LogOperationSuccess(operation, new { userId, Count = notifications.Count });
        return Results.Ok(notifications);
    }
}
