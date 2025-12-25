namespace Core.Models.Identity;

public class League : BaseModel
{
    public required string Name { get; set; }
    public required string OwnerId { get; set; }
    public required string Code { get; set; }
    public required string Format { get; set; }
    public int TotalRounds { get; set; }
    public int CurrentRound { get; set; } = 0;
    public int RoundsToConsider { get; set; }
    public int MinimumRounds { get; set; }
    public double TotalPrize { get; set; } = 0;
    public double PrizePerPerson {  get; set; } = 0;
    public int TotalPlayers { get; set; }
    public List<int>? PointsToGive { get; set;}
    public int? PointsPerWin { get; set;}
    public int? PointsPerDraw { get; set;}
    public int? PointsPerLoss { get; set;}
    public required ScoringSystem ScoringSystem { get; set; } = ScoringSystem.Victories;
    public bool IsActive { get; set; } = true;
    public bool IsPublic { get; set; } = true;
    public ICollection<AppUserLeague> UserLeagues { get; set; } = new List<AppUserLeague>();
    public ICollection<LeagueRoleAssignment> RoleAssignments { get; set; } = new List<LeagueRoleAssignment>();
}


public enum ScoringSystem
{
    Positional,
    Victories
}
