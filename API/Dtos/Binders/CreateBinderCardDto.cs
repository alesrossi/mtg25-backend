using System.ComponentModel.DataAnnotations;

namespace API.Dtos.Binders;

public class CreateBinderCardDto
{
    [Required]
    [Range(1, int.MaxValue)]
    public int CardId { get; set; }

    [Range(0, int.MaxValue)]
    public int QuantityToTrade { get; set; } = 0;

    [StringLength(2000)]
    public string? Notes { get; set; }
}
