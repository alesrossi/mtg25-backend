using Core.Models;
using Core.Models.Identity;

namespace API.Dtos.Binders;

public class BinderCardDto
{
    public int Id { get; set; }
    public int TradeBinderId { get; set; }
    public int CardId { get; set; }
    public required Card Card { get; set; }
    public string Name { get; set; } = string.Empty;
    public int QuantityToTrade { get; set; }
    public int MaxQuantityToTrade { get; set; }
    public string? Notes { get; set; }
    public string? ImageUrl { get; set; }
    public string? SetCode { get; set; }
    public string? SetName { get; set; }
    public string? CollectorNumber { get; set; }
    public string? Rarity { get; set; }
    public double? MarketPrice { get; set; }
    public double? TotalValue { get; set; }
    public MarketProvider? MarketProvider { get; set; }
    public Currency? Currency { get; set; }
}
