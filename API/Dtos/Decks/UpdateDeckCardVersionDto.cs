namespace API.Dtos.Decks;

public class UpdateDeckCardVersionDto
{
    public int MaindeckQuantity { get; set; }
    public int SideboardQuantity { get; set; }
    public int? OwnedCardId { get; set; }
    public required string ScryfallId { get; set; }
}
