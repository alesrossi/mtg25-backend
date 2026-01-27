using System.ComponentModel.DataAnnotations;
using Core.Enums;

namespace Core.Models.Identity;

public class AppUserFriend
{
    [MaxLength(100)]
    public required string UserId { get; set; }
    public required AppUser User { get; set; }

    [MaxLength(100)]
    public required string FriendId { get; set; }
    public required AppUser Friend { get; set; }

    public FriendshipStatus Status { get; set; } = FriendshipStatus.Pending;

    [MaxLength(100)]
    public required string RequestedById { get; set; }
    public required AppUser RequestedBy { get; set; }

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAt { get; set; }
}
