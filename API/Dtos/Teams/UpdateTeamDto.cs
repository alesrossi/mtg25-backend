using System.ComponentModel.DataAnnotations;

namespace API.Dtos.Teams;

public class UpdateTeamDto
{
    [MaxLength(100)]
    public string? Name { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }
}
