using Core.Models.Identity;

namespace Core.Models;

public class Collection : BaseModel
{
    public required string Name { get; set; }
    public required string Color { get; set; }
    public int NumberOfCards { get; set; }
    public double TotalPrice { get; set; }
    public required string OwnerId { get; set; }
}