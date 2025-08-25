namespace API.Dtos;

public class UserWithLeagueInfoDto
{
    public required string DisplayName { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public int Score { get; set; }  // User's score in this league
    public int RoundsPlayed { get; set; }
    public List<int>? Rounds { get; set; } = [];
    public int BestRound { get; set; }
    public double AvgScore { get; set; }
    public int? CurrentRound { get; set; }
}