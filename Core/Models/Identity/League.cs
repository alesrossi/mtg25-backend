namespace Core.Models.Identity;

public class League : BaseModel
{
    public required string Name { get; set; }
    public required string Code { get; set; }
    public required string Format { get; set; }
    public int TotalRounds { get; set; }
    public int RoundsToConsider { get; set; }
    public int MinimumRounds { get; set; }
    public int TotalPlayers { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<AppUserLeague> UserLeagues { get; set; } = new List<AppUserLeague>();
}