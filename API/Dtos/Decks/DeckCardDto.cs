namespace API.Dtos.Decks;

public class DeckCardDto
{
    public int Id { get; set; }
    public int DeckId { get; set; }
    public string OracleId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string SetCode { get; set; } = string.Empty;
    public string? SetName { get; set; }
    public string? ImageUrl { get; set; }
    public string? Rarity { get; set; }
    public string? CollectorNumber { get; set; }
    public int MaindeckQuantity { get; set; }
    public int SideboardQuantity { get; set; }
    public int? OwnedCardId { get; set; }
    public int OwnedQuantity { get; set; }
    public bool IsOwned => OwnedCardId.HasValue;
    public int TotalQuantity => MaindeckQuantity + SideboardQuantity;
    public string OwnershipStatus => GetOwnershipStatus();

    private string GetOwnershipStatus()
    {
        return OwnedQuantity switch
        {
            0 => "NotOwned",
            >= 1 and <= 3 => "PartiallyOwned", 
            >= 4 => "FullyOwned",
            _ => "NotOwned"
        };
    }
}