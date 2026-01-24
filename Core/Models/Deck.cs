using Core.Models.Identity;

namespace Core.Models;

public class Deck : BaseModel
{
    public required string Name { get; set; }
    public required string Format { get; set; }
    public int NumberOfCards { get; set; }
    public int NumberOfMainBoardCards { get; set; }
    public int NumberOfSideBoardCards { get; set; }
    public double TotalPrice { get; set; }
    public Currency? TotalPriceCurrency { get; set; }
    public required string OwnerId { get; set; }
    public string? Image { get; set; }
    public List<string> ColorIdentity { get; set; } = [];

    public int? CurrentBranchId { get; set; }
    public DeckBranch? CurrentBranch { get; set; }
    public int? CurrentCommitId { get; set; }
    public DeckCommit? CurrentCommit { get; set; }
    
    // Navigation property to deck cards
    public ICollection<DeckCard> DeckCards { get; set; } = new List<DeckCard>();

    public ICollection<DeckCommit> Commits { get; set; } = new List<DeckCommit>();
    public ICollection<DeckBranch> Branches { get; set; } = new List<DeckBranch>();
}
