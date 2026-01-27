using System.ComponentModel.DataAnnotations;
using Core.Enums;

namespace Core.Models;

public class WishlistCard : BaseModel
{
    public int WishlistId { get; set; }
    public Wishlist Wishlist { get; set; } = null!;
    [MaxLength(100)]
    public required string ScryfallId { get; set; }
    public bool ExactVersion { get; set; }
    [MaxLength(200)]
    public required string Name { get; set; }
    [MaxLength(500)]
    public string? Notes { get; set; }
    public int DesiredQuantity { get; set; }
    [MaxLength(200)]
    public string? ImageUrl { get; set; }
    [MaxLength(200)]
    public string? BackImageUrl { get; set; }
    [MaxLength(200)]
    public string? ArtCrop { get; set; }
    public bool? IsFoil { get; set; }
    public Language? Language { get; set; }
    public Condition? MinimumCondition { get; set; }
    public int? OriginalDeckId { get; set; }
}
