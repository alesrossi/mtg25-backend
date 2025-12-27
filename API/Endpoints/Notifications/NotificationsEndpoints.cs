using API.Dtos.Notifications;
using API.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints.Notifications;

public static partial class NotificationsEndpoints
{
    public static void MapNotificationsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/notifications")
            .WithTags("Notifications")
            .WithProblemDetailsContract();

        MapNotificationsQueries(group);
        MapNotificationsCommands(group);
    }
    
    private static void MapNotificationsQueries(RouteGroupBuilder group)
    {
        group.MapGet("/{id:int}", GetNotificationFromIdAsync)
            .RequireAuthorization()
            .WithSummary("Retrieves notification")
            .WithDescription("Retrieves notification from given id")
            .Produces<NotificationDto>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");
        
        group.MapGet("/", GetAllUserNotificationsAsync)
            .RequireAuthorization()
            .WithSummary("Retrieves all notifications for logged in user")
            .WithDescription("Retrieves all notifications for logged in user")
            .Produces<IReadOnlyList<NotificationDto>>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");
        
    }
    
    private static void MapNotificationsCommands(RouteGroupBuilder group)
    {
        group.MapDelete("/{id:int}", DeleteNotificationAsync)
            .RequireAuthorization()
            .WithSummary("Deletes a notification from Id")
            .WithDescription("Deletes a notification from Id")
            .Produces<NotificationDto>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");
        
        group.MapPut("/read", UpdateNotificationStatusAsync)
            .RequireAuthorization()
            .WithSummary("Reads notifications")
            .WithDescription("Sets the isRead status of a list of notifications from false to true")
            .Produces<NotificationDto>()
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");
        
        group.MapPut("/approve", ApproveNotificationAsync)
            .RequireAuthorization()
            .WithSummary("Approves notification from Id")
            .WithDescription("Sets the approval of a notification from false to true")
            .Produces<NotificationDto>()
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");
    }
}