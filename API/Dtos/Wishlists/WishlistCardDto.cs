using Core.Models;
using Core.Enums;

namespace API.Dtos.Wishlists;

public class WishlistCardDto
{
    public int Id { get; set; }
    public int WishlistId { get; set; }
    public string ScryfallId { get; set; } = string.Empty;
    public bool ExactVersion { get; set; } 
    public string Name { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? BackImageUrl { get; set; }
    public string? ArtCrop { get; set; }
    public int DesiredQuantity { get; set; }
    public bool? IsFoil { get; set; }
    public CardLanguage? Language { get; set; }
    public Condition? MinimumCondition { get; set; }
    public bool IsAny { get; set; }
    public string? Notes { get; set; }
    public int? OriginalDeckId { get; set; }
}
