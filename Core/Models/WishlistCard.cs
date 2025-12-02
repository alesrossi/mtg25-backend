namespace Core.Models;

public class WishlistCard : BaseModel
{
    public int WishlistId { get; set; }
    public Wishlist Wishlist { get; set; } = null!;
    public required string OracleId { get; set; }
    public required string Name { get; set; }
    public string? ImageUrl { get; set; }
    public string? BackImageUrl { get; set; }
    public int DesiredQuantity { get; set; }
    public bool? IsFoil { get; set; }
    public string? Language { get; set; }
    public string Notes { get; set; }
    public int? OriginalDeckId { get; set; }
}
