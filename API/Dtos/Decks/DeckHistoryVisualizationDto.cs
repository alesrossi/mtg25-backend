namespace API.Dtos.Decks;

public class DeckHistoryVisualizationDto
{
    public int DeckId { get; set; }
    public IReadOnlyList<BranchHistoryDto> Branches { get; set; } = [];
    public IReadOnlyList<CommitNodeDto> OrphanedCommits { get; set; } = [];
    public CommitGraphMetadataDto Metadata { get; set; } = new();
}

public class BranchHistoryDto
{
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public int? HeadCommitId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public IReadOnlyList<CommitNodeDto> Commits { get; set; } = [];
}

public class CommitNodeDto
{
    public int Id { get; set; }
    public int DeckId { get; set; }
    public int TreeId { get; set; }
    public string AuthorId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CommittedAt { get; set; }
    public IReadOnlyList<int> ParentIds { get; set; } = [];
    public IReadOnlyList<string> ReferencingBranches { get; set; } = [];
}

public class CommitGraphMetadataDto
{
    public int TotalCommits { get; set; }
    public int TotalBranches { get; set; }
    public int OrphanedCommits { get; set; }
    public DateTime? EarliestCommit { get; set; }
    public DateTime? LatestCommit { get; set; }
}
