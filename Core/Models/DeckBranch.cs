namespace Core.Models;

public class DeckBranch : BaseModel
{
    public int DeckId { get; set; }
    public Deck Deck { get; set; } = null!;

    public required string Name { get; set; }
    public int? HeadCommitId { get; set; }
    public DeckCommit? HeadCommit { get; set; }

    public required string CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
