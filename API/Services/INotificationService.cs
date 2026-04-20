namespace API.Services;

public interface INotificationService
{
    Task SendNotificationAsync(string userId, string notificationType, object? data = null, CancellationToken cancellationToken = default);
}
