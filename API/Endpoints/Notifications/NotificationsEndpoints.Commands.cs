using API.Dtos.Notifications;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints.Notifications;

public static partial class NotificationsEndpoints
{
    private static void MapNotificationsCommands(RouteGroupBuilder group)
    {
        group.MapPost("/{id:int}", DeleteNotificationAsync)
            .RequireAuthorization()
            .WithSummary("Deletes a notification from Id")
            .WithDescription("Deletes a notification from Id")
            .Produces<NotificationDto>()
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json");
        
        group.MapPut("/{id:int}", UpdateNotificationStatusAsync)
            .RequireAuthorization()
            .WithSummary("Reads notification from Id")
            .WithDescription("Sets the isRead status of a notification from false to true")
            .Produces<NotificationDto>()
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json");
    }
    
    private static async Task<IResult> DeleteNotificationAsync()
    {
        throw new NotImplementedException();
    }
    
    private static async Task<IResult> UpdateNotificationStatusAsync(int id)
    {
        throw new NotImplementedException();
    }

}