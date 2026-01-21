namespace Core.Models;

public class DeckCommit : BaseModel
{
    public int DeckId { get; set; }
    public Deck Deck { get; set; } = null!;

    public int TreeId { get; set; }
    public DeckTree Tree { get; set; } = null!;

    public required string AuthorId { get; set; }
    public required string Message { get; set; }
    public DateTime CommittedAt { get; set; } = DateTime.UtcNow;

    public byte[]? ContentHash { get; set; }

    public ICollection<DeckCommitParent> ParentLinks { get; set; } = new List<DeckCommitParent>();
    public ICollection<DeckCommitParent> ChildLinks { get; set; } = new List<DeckCommitParent>();
}
