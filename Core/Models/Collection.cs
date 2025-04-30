namespace Core.Models;

public class Collection : BaseModel
{
    public required string Name { get; set; }
    public required string Color { get; set; }
    public int NumberOfCards { get; set; }
    public double TotalPrice { get; set; }
}