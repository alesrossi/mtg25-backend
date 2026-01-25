using Core.Enums;

namespace API.Dtos.Cards;

public class UpdateCollectionCardWithSFIdDto
{
    public int CollectionId { get; set; }
    public int Quantity { get; set; }
    public required Language Language { get; set; }
    public required string Condition { get; set; } 
    public bool IsFoil { get; set; }
    public double PurchasePrice { get; set; }
    public required string PurchasePriceCurrency { get; set; }
    public required bool IsMisprint { get; set; }
    public required bool IsAltered  { get; set; }
    public required string ScryfallId  { get; set; }
}
