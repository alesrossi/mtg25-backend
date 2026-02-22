using System;

namespace API.Dtos.Decks;

public class DeckBranchDto
{
    public int Id { get; set; }
    public int DeckId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? HeadCommitId { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
