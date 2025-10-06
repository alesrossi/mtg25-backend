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
    public required string OwnerId { get; set; }
    
    // Navigation property to deck cards
    public ICollection<DeckCard> DeckCards { get; set; } = new List<DeckCard>();
}