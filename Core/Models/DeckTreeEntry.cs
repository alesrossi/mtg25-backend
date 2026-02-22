namespace Core.Models;

public class DeckTreeEntry : BaseModel
{
    public int TreeId { get; set; }
    public DeckTree Tree { get; set; } = null!;

    public required string ScryfallId { get; set; }

    public int MaindeckQuantity { get; set; }
    public int SideboardQuantity { get; set; }

    public int TotalQuantity => MaindeckQuantity + SideboardQuantity;
}
