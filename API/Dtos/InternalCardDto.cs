namespace API.Dtos;

public class InternalCardDto
{
    public required string OracleId { get; set; }
    public int CollectionId { get; set; }
    public int Quantity { get; set; }
    public required string Language { get; set; }
    public required string Version { get; set; } 
    public required string Condition { get; set; } 
    public bool IsFoil { get; set; }
    public double PurchasePrice { get; set; }
    public required string PurchasePriceCurrency { get; set; }
    public required bool IsMisprint { get; set; }
    public required bool IsAltered  { get; set; }
}