namespace API.Dtos.Binders;

public class BinderCardDto
{
    public int Id { get; set; }
    public int TradeBinderId { get; set; }
    public int CardId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int QuantityToTrade { get; set; }
    public string? Notes { get; set; }
    public string? ImageUrl { get; set; }
    public string? SetCode { get; set; }
    public string? SetName { get; set; }
    public string? CollectorNumber { get; set; }
    public string? Rarity { get; set; }
}
