using System.ComponentModel.DataAnnotations;

namespace Core.Models;

public class TradeBinder : BaseModel
{
    public required string Name { get; set; }
    [MaxLength(1000)]
    public string? Description { get; set; }
    public required bool IsPublic { get; set; }
    [MaxLength(100)]
    public required string OwnerId { get; set; }
    public ICollection<BinderCard> BinderCards { get; set; } = new List<BinderCard>();
}
