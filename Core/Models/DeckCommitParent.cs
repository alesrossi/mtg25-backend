namespace Core.Models;

public class DeckCommitParent : BaseModel
{
    public int CommitId { get; set; }
    public DeckCommit Commit { get; set; } = null!;

    public int ParentCommitId { get; set; }
    public DeckCommit ParentCommit { get; set; } = null!;
}
