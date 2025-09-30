using System.ComponentModel.DataAnnotations;

namespace API.Dtos.Wishlists;

public class CreateWishlistCardDto
{
    [Required]
    public string OracleId { get; set; } = string.Empty;
    [Range(1, 999)]
    public int DesiredQuantity { get; set; } = 1;
    public bool? IsFoil { get; set; }
    [StringLength(50)]
    public string? Language { get; set; }
    [StringLength(500)]
    public required string Notes { get; set; }
}
