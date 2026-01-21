using System.ComponentModel.DataAnnotations;

namespace API.Dtos.Leagues;

public class UpdateRoundDto
{
    public DateTime? StartDate { get; set; }

    [StringLength(256, ErrorMessage = "Description must be at most 256 characters")]
    public string? Description { get; set; }

    public List<UpdateRoundPlayerDto>? Players { get; set; }
}
