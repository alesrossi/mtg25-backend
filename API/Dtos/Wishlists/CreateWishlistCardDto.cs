using System.ComponentModel.DataAnnotations;
using Core.Models;

namespace API.Dtos.Wishlists;

public class CreateWishlistCardDto
{
    [Required]
    public required string ScryfallId { get; set; }
    [Range(1, 999)]
    public int DesiredQuantity { get; set; } = 1;
    public bool ExactVersion { get; set; }
    public bool? IsFoil { get; set; }
    [StringLength(50)]
    public string? Language { get; set; }
    public Condition? MinimumCondition { get; set; }
    [StringLength(500)]
    public string? Notes { get; set; }
    public int? OriginalDeckId { get; set; }
}
