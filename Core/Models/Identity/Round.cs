
using System.ComponentModel.DataAnnotations;

namespace Core.Models.Identity;

public class Round : BaseModel
{
    public Status Status { get; set; }
    public DateTime?  StartDate { get; set; }
    [MaxLength(500)]
    public string? Description { get; set; }
    public int Order { get; set; }
    public int LeagueId { get; set; }
    public required League League { get; set; }
    public List<AppUserRound> Players { get; set; } = [];
    public List<RoundParticipant> Participants { get; set; } = [];
}

public enum Status
{
    NotPlayed,
    Playing,
    Played
}
