using System.ComponentModel.DataAnnotations;

namespace Core.Models.Identity;

public class Notification: BaseModel
{
    [MaxLength(100)]
    public required string Name { get; set; }
    [MaxLength(500)]
    public required string Message { get; set; }
    [MaxLength(100)]
    public string? MessageKey { get; set; }
    [MaxLength(1000)]
    public string? MessageArgsJson { get; set; }
    public bool IsRead { get; set; } = false;
    public bool Approval { get; set; } = false;
    [MaxLength(100)]
    public string? ObjectId { get; set; }
    [MaxLength(500)]
    public required string Origin { get; set; }
    public DateTime CreationDateTime {  get; set; }
    [MaxLength(100)]
    public required string AppUserId { get; set; }
    public required AppUser AppUser { get; set; }
}
