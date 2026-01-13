namespace Core.Models.Identity;

public class Notification: BaseModel
{
    public required string Name { get; set; }
    public required string Message { get; set; }
    public string? MessageKey { get; set; }
    public string? MessageArgsJson { get; set; }
    public bool IsRead { get; set; } = false;
    public bool Approval { get; set; } = false;
    public string? ObjectId { get; set; }
    public required string Origin { get; set; }
    public DateTime CreationDateTime {  get; set; }
    public required string AppUserId { get; set; }
    public required AppUser AppUser { get; set; }
}
