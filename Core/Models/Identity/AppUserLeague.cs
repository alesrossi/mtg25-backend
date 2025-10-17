namespace Core.Models.Identity;

public class AppUserLeague
{
    public string UserId { get; set; }
    public AppUser User { get; set; }
    public int LeagueId { get; set; }
    public League League { get; set; }
    public int Score { get; set; }  // User's score in this league
    public int RoundsPlayed { get; set; }
    public List<int> Rounds { get; set; } = [];
    public int BestRound { get; set; }
    public double AvgScore { get; set; }
    public bool IsPlaying {get; set; } = true;
}