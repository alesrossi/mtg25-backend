using Core.Enums;

namespace Core.Models;

public class Wishlist : BaseModel
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required bool IsPublic { get; set; }
    public required string OwnerId { get; set; }
    public double TotalPrice { get; set; }
    public Currency? TotalPriceCurrency { get; set; }
    public ICollection<WishlistCard> WishlistCards { get; set; } = new List<WishlistCard>();
}
