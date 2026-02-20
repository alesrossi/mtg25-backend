using System;

namespace API.Dtos.Decks;

public class DeckCommitDto
{
    public int Id { get; set; }
    public int DeckId { get; set; }
    public int TreeId { get; set; }
    public string AuthorId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CommittedAt { get; set; }
}
