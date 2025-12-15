namespace API.Dtos.Notifications;

public class NewNotificationDto
{
    public required string Name { get; set; }
    public required string Message { get; set; }
    public int? ObjectId { get; set; }
    public required string Origin { get; set; }
    public required string AppUserId { get; set; }
}