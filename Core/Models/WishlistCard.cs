namespace Core.Models;

public class WishlistCard : BaseModel
{
    public int WishlistId { get; set; }
    public Wishlist Wishlist { get; set; } = null!;
    public required string OracleId { get; set; }
    public required string Name { get; set; }
    public required string SetCode { get; set; }
    public string? SetName { get; set; }
    public string? ImageUrl { get; set; }
    public string? CollectorNumber { get; set; }
    public string? Rarity { get; set; }
    public int DesiredQuantity { get; set; }
    public bool IsFoil { get; set; }
    public string? Language { get; set; }
    public string? Notes { get; set; }
}
