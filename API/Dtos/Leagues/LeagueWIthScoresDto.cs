namespace API.Dtos.Leagues;

public class LeagueWithScoresDto // League from User point of view
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string OwnerId { get; set; }
    public required int CurrentRound { get; set; }
    public List<Score> Scores { get; set; } = [];
}

public class Score
{
    public required string FirstName { get; set; } 
    public required string LastName { get; set; } 
    public required string UserId { get; set; } 
    public required int Points { get; set; }
    public int RoundsPlayed { get; set; }
    public int BestRound { get; set; }
    public double AvgScore { get; set; }
    public required List<int> Rounds { get; set; }
} 