using System.ComponentModel.DataAnnotations;

namespace API.Dtos.Teams;

public class CreateTeamDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }
}
