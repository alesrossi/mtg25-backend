using System.ComponentModel.DataAnnotations;

namespace Core.Models;

public class Collection : BaseModel
{
    [MaxLength(100)]
    public required string Name { get; set; }
    [MaxLength(100)]
    public required string Color { get; set; }
    public int NumberOfCards { get; set; }
    public double TotalPrice { get; set; }
    [MaxLength(100)]
    public required string OwnerId { get; set; }
}