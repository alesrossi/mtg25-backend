namespace API.Dtos.Notifications;

public class NewNotificationDto
{
    public required string Name { get; set; }
    public required string Message { get; set; }
    public string? MessageKey { get; set; }
    public string[]? MessageArgs { get; set; }
    public string? ObjectId { get; set; }
    public required string Origin { get; set; }
    public required string AppUserId { get; set; }
}
