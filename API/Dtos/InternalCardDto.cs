namespace API.Dtos;

public class InternalCardDto
{
    public string Name { get; set; }
    public int CollectionId { get; set; }
    public int Quantity { get; set; }
    public string Language { get; set; }
    public string Version { get; set; } 
    public string Condition { get; set; } 
    public bool IsFoil { get; set; }
    public double PurchasePrice { get; set; }
}