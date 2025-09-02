namespace API.Dtos.Leagues;

public class LeagueDto // League from User point of view
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Code { get; set; }
    public required string Format { get; set; }
    public int TotalRounds { get; set; }
    public int RoundsToConsider { get; set; }
    public int MinimumRounds { get; set; }
    public double TotalPrize { get; set; } = 0;
    public double PrizePerPerson {  get; set; } = 0;
    public int TotalPlayers { get; set; }
    public required int Score { get; set; }
}