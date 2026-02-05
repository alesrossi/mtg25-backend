using Core.Models.Identity;
using Core.Enums;

namespace API.Dtos.Leagues;

public class LeagueDto
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Code { get; set; }
    public required DeckFormat Format { get; set; }
    public int TotalRounds { get; set; }
    public int CurrentRound { get; set; }
    public int CurrentRoundOrder { get; set; }
    public int RoundsToConsider { get; set; }
    public int MinimumRounds { get; set; }
    public double TotalPrize { get; set; }
    public double PrizePerPerson { get; set; }
    public int TotalPlayers { get; set; }
    public required int Score { get; set; }
    public ScoringSystem ScoringSystem { get; set; }
    public bool IsActive { get; set; }
    public bool IsPlaying { get; set; }
    public required string OwnerId { get; set; }
    public bool IsPublic { get; set; }
    public List<string> AdminIds { get; set; } = [];
}
