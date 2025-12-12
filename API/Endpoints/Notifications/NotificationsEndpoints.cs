using API.Extensions;

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
}