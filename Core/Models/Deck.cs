using System.ComponentModel.DataAnnotations;
using Core.Enums;

namespace Core.Models;

public class Deck : BaseModel
{
    [MaxLength(100)]
    public required string Name { get; set; }
    public required DeckFormat Format { get; set; }
    public int NumberOfCards { get; set; }
    public int NumberOfMainBoardCards { get; set; }
    public int NumberOfSideBoardCards { get; set; }
    public double TotalPrice { get; set; }
    public Currency? TotalPriceCurrency { get; set; }
    [MaxLength(100)]
    public required string OwnerId { get; set; }
    [MaxLength(300)]
    public string? Image { get; set; }
    public List<string> ColorIdentity { get; set; } = [];
    public required string DeckList { get; set; } = string.Empty;
    public required bool IsPublic { get; set; } = true;
    public int? CurrentBranchId { get; set; }
    public DeckBranch? CurrentBranch { get; set; }
    public int? CurrentCommitId { get; set; }
    public DeckCommit? CurrentCommit { get; set; }
    
    // Navigation property to deck cards
    public ICollection<DeckCard> DeckCards { get; set; } = new List<DeckCard>();

    public ICollection<DeckCommit> Commits { get; set; } = new List<DeckCommit>();
    public ICollection<DeckBranch> Branches { get; set; } = new List<DeckBranch>();
}
