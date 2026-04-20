using Core.Enums;

namespace API.Dtos.Decks;

public class DeckDto
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required DeckFormat Format { get; set; }
    public int NumberOfCards { get; set; }
    public int NumberOfMainBoardCards { get; set; }
    public int NumberOfSideBoardCards { get; set; }
    public double TotalPrice { get; set; }
    public Currency? TotalPriceCurrency { get; set; }
    public string? Image { get; set; }
    public List<string> ColorIdentity { get; set; } = [];
    public bool IsPublic { get; set; }
    public required string OwnerId { get; set; }
    public int? CurrentBranchId { get; set; }
    public int? CurrentCommitId { get; set; }
    public int? TeamId { get; set; }
    public string? TeamName { get; set; }
}
