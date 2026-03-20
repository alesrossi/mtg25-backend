using System.ComponentModel.DataAnnotations;
using Core.Models;
using Core.Enums;

namespace API.Dtos.Wishlists;

public class UpdateWishlistCardDto
{
    [StringLength(50)]
    public string? ScryfallId { get; set; }
    public bool? ExactVersion { get; set; }
    [Range(1, 999)]
    public int? DesiredQuantity { get; set; } = 1;
    public bool? IsFoil { get; set; }
    public CardLanguage? Language { get; set; }
    public Condition? MinimumCondition { get; set; }
    [StringLength(500)]
    public string? Notes { get; set; }
}
