using System.ComponentModel.DataAnnotations;

namespace API.Dtos.Leagues;

public class UpdateLeagueDto
{
    [StringLength(32, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 32 characters")]
    public string? Name { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Only positive numbers are allowed")]
    public int? TotalRounds { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Only positive numbers are allowed")]
    public int? RoundsToConsider { get; set; }
    [Range(0, int.MaxValue, ErrorMessage = "Only positive numbers are allowed")]
    public int? MinimumRounds { get; set; }
    public List<int>? PointsToGive {get; set;}
    [Range(0, int.MaxValue, ErrorMessage = "Only positive numbers are allowed")]
    public double? TotalPrize { get; set; }
    [Range(0, int.MaxValue, ErrorMessage = "Only positive numbers are allowed")]
    public double? PrizePerPerson {  get; set; } 
    [Range(0, int.MaxValue, ErrorMessage = "Only positive numbers are allowed")]
    public int? CurrentRound {  get; set; } 
    public bool? IsPublic { get; set; }
}
