namespace Core.Models.Identity;

public class Notification: BaseModel
{
    public required string Name { get; set; }
    public required string Message { get; set; }
    public bool IsRead { get; set; }
    public bool IsInstant { get; set; }
    public required string Origin { get; set; }
    public DateTime CreationDateTime {  get; set; }
    public required string AppUserId { get; set; }
    public required AppUser AppUser { get; set; }
}