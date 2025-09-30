using System.ComponentModel.DataAnnotations;

namespace API.Dtos.Wishlists;

public class UpdateWishlistCardDto
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(10)]
    public string SetCode { get; set; } = string.Empty;

    [StringLength(200)]
    public string? SetName { get; set; }

    [Url]
    public string? ImageUrl { get; set; }

    [StringLength(50)]
    public string? CollectorNumber { get; set; }

    [StringLength(50)]
    public string? Rarity { get; set; }

    [Range(1, 999)]
    public int DesiredQuantity { get; set; } = 1;

    public bool IsFoil { get; set; }

    [StringLength(50)]
    public string? Language { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}
