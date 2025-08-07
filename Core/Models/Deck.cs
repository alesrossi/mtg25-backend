using Core.Models.Identity;

namespace Core.Models;

public class Deck : BaseModel
{
    public required string Name { get; set; }
    public required string Format { get; set; }
    public int NumberOfCards { get; set; }
    public double TotalPrice { get; set; }
    public required AppUser Owner { get; set; }
}