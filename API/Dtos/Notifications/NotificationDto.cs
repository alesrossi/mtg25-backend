namespace API.Dtos.Notifications;

public class NotificationDto
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Message { get; set; }
    public bool IsRead { get; set; }
    public bool Approval { get; set; }
    public required string Origin { get; set; }
    public DateTime CreationDateTime {  get; set; }
}