using Core.Enums;

namespace API.Dtos.Cards;

public class CardDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ScryfallId { get; set; } = string.Empty;
    public string OracleId { get; set; } = string.Empty;
    public int CollectionId { get; set; }
    public int Quantity { get; set; }
    public CardLanguage Language { get; set; }
    public string Condition { get; set; } = string.Empty;
    public bool IsFoil { get; set; }
    public double PurchasePrice { get; set; }
    public Currency PurchasePriceCurrency { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string? BackImageUrl { get; set; }
    public string ArtCrop { get; set; } = string.Empty;
    public string SetCode { get; set; } = string.Empty;
    public string SetName { get; set; } = string.Empty;
    public string TypeLine { get; set; } = string.Empty;
    public string CollectorNumber { get; set; } = string.Empty;
    public string Rarity { get; set; } = string.Empty;
    public bool IsMisprint { get; set; }
    public bool IsAltered { get; set; }
}
