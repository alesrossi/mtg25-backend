using Core.Enums;

namespace API.Dtos.Teams;

public class TeamMemberDto
{
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public TeamRole Role { get; set; }
    public DateTime JoinedAt { get; set; }
    public string? InvitedByDisplayName { get; set; }
}
