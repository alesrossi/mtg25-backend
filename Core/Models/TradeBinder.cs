using System.Collections.Generic;

namespace Core.Models;

public class TradeBinder : BaseModel
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required bool IsPublic { get; set; }
    public required string OwnerId { get; set; }
    public ICollection<BinderCard> BinderCards { get; set; } = new List<BinderCard>();
}
