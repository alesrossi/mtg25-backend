namespace API.Dtos.Decks;

public class CreateDeckCardDto
{
    public required string OracleId { get; set; }
    public required string Name { get; set; }
    public required string SetCode { get; set; }
    public string? SetName { get; set; }
    public required string ImageUrl { get; set; }
    public string? Rarity { get; set; }
    public string? CollectorNumber { get; set; }
    public int MaindeckQuantity { get; set; }
    public int SideboardQuantity { get; set; }
    public int? OwnedCardId { get; set; }
}