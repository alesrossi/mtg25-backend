using System.ComponentModel.DataAnnotations;

namespace API.Dtos.Leagues;

public class NewLeagueDto
{
    [Required(ErrorMessage = "Name is required")]
    [StringLength(32, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 32 characters")]
    public required string Name { get; set; }
    [Required(ErrorMessage = "Format is required")]
    public required string Format { get; set; }
    [Required(ErrorMessage = "TotalRounds is required")]
    [Range(1, int.MaxValue, ErrorMessage = "Only positive numbers are allowed")]
    public int TotalRounds { get; set; }
    [Required(ErrorMessage = "RoundsToConsider is required")]
    [Range(1, int.MaxValue, ErrorMessage = "Only positive numbers are allowed")]
    public int RoundsToConsider { get; set; }
    [Required(ErrorMessage = "MinimumRounds is required")]
    [Range(0, int.MaxValue, ErrorMessage = "Only positive numbers are allowed")]
    public int MinimumRounds { get; set; }
    public required List<int> PointsToGive {get; set;}
    public double? TotalPrize { get; set; }
    public double PrizePerPerson {  get; set; } 
}