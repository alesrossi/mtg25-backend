using System.ComponentModel.DataAnnotations;

namespace Core.Models;

public class DeckCard : BaseModel
{
    public int DeckId { get; set; }
    public Deck Deck { get; set; } = null!;
    
    // Card reference (Scryfall data)
    [MaxLength(100)]
    public required string ScryfallId { get; set; }
    [MaxLength(200)]
    public required string Name { get; set; }
    [MaxLength(100)]
    public required string SetCode { get; set; }
    [MaxLength(100)]
    public string? SetName { get; set; }
    public List<string> ColorIdentity { get; set; } = [];
    [MaxLength(300)]
    public string? ImageUrl { get; set; }
    [MaxLength(300)]
    public string? BackImageUrl { get; set; }
    [MaxLength(300)]
    public string? ArtCrop { get; set; }
    [MaxLength(100)]
    public string? Rarity { get; set; }
    [MaxLength(100)]
    public string? CollectorNumber { get; set; }
    [MaxLength(200)]
    public required string TypeLine { get; set; }
    
    // Deck-specific properties
    public int MaindeckQuantity { get; set; }
    public int SideboardQuantity { get; set; }
    
    // Optional: link to owned cards if user owns them
    public int? OwnedCardId { get; set; }
    public Card? OwnedCard { get; set; }
    
    // Calculated property for total quantity
    public int TotalQuantity => MaindeckQuantity + SideboardQuantity;
}
