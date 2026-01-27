using System.ComponentModel.DataAnnotations;

namespace Core.Models.Identity;

public class AppUserLeague
{
    [MaxLength(100)]
    public required string UserId { get; set; }
    [MaxLength(100)]
    public AppUser? User { get; set; }
    public int LeagueId { get; set; }
    [MaxLength(100)]
    public League? League { get; set; }
    public int Score { get; set; }  // User's score in this league
    public int RoundsPlayed { get; set; }
    public List<int> Rounds { get; set; } = [];
    public int BestRound { get; set; }
    public double AvgPosition { get; set; }
    public bool IsPlaying {get; set; } = true;
}
