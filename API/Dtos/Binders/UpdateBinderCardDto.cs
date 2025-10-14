using System.ComponentModel.DataAnnotations;

namespace API.Dtos.Binders;

public class UpdateBinderCardDto
{
    [Range(0, int.MaxValue)]
    public int QuantityToTrade { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }
}
