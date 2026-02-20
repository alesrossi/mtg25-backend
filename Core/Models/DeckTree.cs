namespace Core.Models;

public class DeckTree : BaseModel
{
    public byte[]? TreeHash { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<DeckTreeEntry> Entries { get; set; } = new List<DeckTreeEntry>();
}
