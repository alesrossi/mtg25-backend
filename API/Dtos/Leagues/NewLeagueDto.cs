using System.ComponentModel.DataAnnotations;
using Core.Models.Identity;

using Core.Enums;

namespace API.Dtos.Leagues;

public class NewLeagueDto
{
    [Required(ErrorMessage = "Name is required")]
    [StringLength(32, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 32 characters")]
    public required string Name { get; set; }
    [Required(ErrorMessage = "Format is required")]
    public required DeckFormat Format { get; set; }
    [Required(ErrorMessage = "TotalRounds is required")]
    [Range(1, int.MaxValue, ErrorMessage = "Only positive numbers are allowed")]
    public int TotalRounds { get; set; }
    [Required(ErrorMessage = "RoundsToConsider is required")]
    [Range(1, int.MaxValue, ErrorMessage = "Only positive numbers are allowed")]
    public int RoundsToConsider { get; set; }
    [Required(ErrorMessage = "MinimumRounds is required")]
    [Range(0, int.MaxValue, ErrorMessage = "Only positive numbers are allowed")]
    public int MinimumRounds { get; set; }
    public List<int>? PointsToGive {get; set;}
    public int? PointsPerWin { get; set;}
    public int? PointsPerDraw { get; set;}
    public int? PointsPerLoss { get; set;}
    public required ScoringSystem ScoringSystem { get; set; } 
    public double? TotalPrize { get; set; }
    public double PrizePerPerson {  get; set; } 
    public bool IsPublic { get; set; } = true;
}
