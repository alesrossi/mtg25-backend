using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Core.Enums;
using Core.Models.Identity;

namespace Core.Models;

public class TeamMember
{
    [Required]
    [MaxLength(100)]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey(nameof(UserId))]
    public AppUser User { get; set; } = null!;

    public int TeamId { get; set; }

    [ForeignKey(nameof(TeamId))]
    public Team Team { get; set; } = null!;

    public TeamRole Role { get; set; } = TeamRole.Member;

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string? InvitedById { get; set; }

    [ForeignKey(nameof(InvitedById))]
    public AppUser? InvitedBy { get; set; }
}
