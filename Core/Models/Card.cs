namespace Core.Models;

public class Card : BaseModel
{
    public required string Name { get; set; }
    public required string OracleId { get; set; }
    public int CollectionId { get; set; }
    public Collection? Collection { get; set; }
    public required int Quantity { get; set; }
    public required string Language { get; set; }
    public required string Version { get; set; }
    public Condition Condition  { get; set; }
    public required bool IsFoil { get; set; }
    public required double PurchasePrice { get; set; }
    public required string PurchasePriceCurrency { get; set; }
    public required string ImageUrl { get; set; }
    public required string ArtCrop { get; set; }
    public required string SetCode { get; set; }
    public required string SetName { get; set; }
    public required string CollectorNumber  { get; set; }
    public required string Rarity { get; set; }
    public required bool IsMisprint { get; set; }
    public required bool IsAltered  { get; set; }
}

public enum Condition
{
    Mint,
    NearMint,
    Excellent,
    Good,
    LightPlayed,
    Played,
    Poor
}