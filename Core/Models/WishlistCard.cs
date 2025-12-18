namespace Core.Models;

public class WishlistCard : BaseModel
{
    public int WishlistId { get; set; }
    public Wishlist Wishlist { get; set; } = null!;
    public required string ScryfallId { get; set; }
    public bool ExactVersion { get; set; }
    public required string Name { get; set; }
    public string? Notes { get; set; }
    public int DesiredQuantity { get; set; }
    public string? ImageUrl { get; set; }
    public string? BackImageUrl { get; set; }
    public string? ArtCrop { get; set; }
    public bool? IsFoil { get; set; }
    public string? Language { get; set; }
    public Condition? MinimumCondition { get; set; }
    public int? OriginalDeckId { get; set; }
}
