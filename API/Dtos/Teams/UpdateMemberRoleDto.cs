using System.ComponentModel.DataAnnotations;
using Core.Enums;

namespace API.Dtos.Teams;

public class UpdateMemberRoleDto
{
    [Required]
    public TeamRole NewRole { get; set; }
}
