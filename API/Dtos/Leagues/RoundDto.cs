using Core.Models.Identity;

namespace API.Dtos.Leagues;

public class RoundDto
{
    public int Id { get; set; }
    public Status Status { get; set; }
    public DateTime? StartDate { get; set; }
    public string? Description { get; set; }
    public int Order { get; set; }
    public int LeagueId { get; set; }
    public List<AppUserRoundDto> Players { get; set; } = [];
}
