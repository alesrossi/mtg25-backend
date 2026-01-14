
namespace Core.Models.Identity;

public class Round : BaseModel
{
    public Status Status { get; set; }
    public DateTime?  StartDate { get; set; }
    public string? Description { get; set; }
    public int LeagueId { get; set; }
    public required League League { get; set; }
    public List<AppUserRound> Players { get; set; } = [];
}

public enum Status
{
    NotPlayed,
    Playing,
    Played
}