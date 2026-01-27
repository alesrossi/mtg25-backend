using System.ComponentModel.DataAnnotations;

namespace Core.Models;

public class BinderCard : BaseModel
{
    public int TradeBinderId { get; set; }
    public TradeBinder TradeBinder { get; set; } = null!;
    public required string Name { get; set; }
    public int QuantityToTrade { get; set; }
    [MaxLength(1000)]
    public string? Notes { get; set; }
    public int CardId { get; set; }
    public Card? Card { get; set; }
}
