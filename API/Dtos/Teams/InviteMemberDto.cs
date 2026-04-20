using System.ComponentModel.DataAnnotations;

namespace API.Dtos.Teams;

public class InviteMemberDto
{
    [Required]
    public string TargetUserId { get; set; } = string.Empty;
}
