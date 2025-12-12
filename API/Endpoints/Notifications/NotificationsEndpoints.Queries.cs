using API.Dtos.Notifications;
using Microsoft.AspNetCore.Mvc;

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

        group.MapGet("/instant", GetAllInstantUserNotificationsAsync)
            .RequireAuthorization()
            .WithSummary("Retrieves all instant notifications for logged in user")
            .WithDescription("Retrieves all instant notifications for logged in user these are cached on Redis therefore this route can be called more often and ")
            .Produces<IReadOnlyList<NotificationDto>>()
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json");
    }
    
    private static async Task<IResult> GetNotificationFromIdAsync(int id)
    {
        throw new NotImplementedException();
    }
    
    private static async Task<IResult> GetAllUserNotificationsAsync()
    {
        throw new NotImplementedException();
    }
    
    private static async Task<IResult> GetAllInstantUserNotificationsAsync()
    {
        throw new NotImplementedException();
    }
}