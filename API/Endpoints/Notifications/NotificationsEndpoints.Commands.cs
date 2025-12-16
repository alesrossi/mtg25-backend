using System.Security.Claims;
using API.Dtos.Notifications;
using API.Endpoints.Leagues;
using API.Logging;
using API.Services;
using Core.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints.Notifications;

public static partial class NotificationsEndpoints
{
    private static void MapNotificationsCommands(RouteGroupBuilder group)
    {
        group.MapDelete("/{id:int}", DeleteNotificationAsync)
            .RequireAuthorization()
            .WithSummary("Deletes a notification from Id")
            .WithDescription("Deletes a notification from Id")
            .Produces<NotificationDto>()
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json");
        
        group.MapPut("/read", UpdateNotificationStatusAsync)
            .RequireAuthorization()
            .WithSummary("Reads notifications")
            .WithDescription("Sets the isRead status of a list of notifications from false to true")
            .Produces<NotificationDto>()
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json");
        
        group.MapPut("/approve", ApproveNotificationAsync)
            .RequireAuthorization()
            .WithSummary("Approves notification from Id")
            .WithDescription("Sets the approval of a notification from false to true")
            .Produces<NotificationDto>()
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json");
    }
    
    private static async Task<IResult> DeleteNotificationAsync(
        int id,
        HttpContext context,
        [FromServices] UserManager<AppUser> userManager,
        [FromServices] NotificationService notificationService,
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
            return Results.NotFound("Notification not found");
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

        var successful = await notificationService.UpdateNotificationAsync(notificationIds, true, null);
        
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

        var successful = await notificationService.UpdateNotificationAsync(notificationIds, null, true);
        
        return Results.Ok();
    }
}