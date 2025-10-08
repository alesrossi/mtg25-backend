namespace Core.Models;

public class DeckCard : BaseModel
{
    public int DeckId { get; set; }
    public Deck Deck { get; set; } = null!;
    
    // Card reference (Scryfall data)
    public required string OracleId { get; set; }
    public required string Name { get; set; }
    public required string SetCode { get; set; }
    public string? SetName { get; set; }
    public List<string> ColorIdentity { get; set; } = [];
    public string? ImageUrl { get; set; }
    public string? Rarity { get; set; }
    public string? CollectorNumber { get; set; }
    
    // Deck-specific properties
    public int MaindeckQuantity { get; set; }
    public int SideboardQuantity { get; set; }
    
    // Optional: link to owned cards if user owns them
    public int? OwnedCardId { get; set; }
    public Card? OwnedCard { get; set; }
    
    // Calculated property for total quantity
    public int TotalQuantity => MaindeckQuantity + SideboardQuantity;
}