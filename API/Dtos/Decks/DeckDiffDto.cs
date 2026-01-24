using System;
using System.Collections.Generic;

namespace API.Dtos.Decks;

public class DeckDiffDto
{
    public IReadOnlyList<DeckDiffEntryDto> Added { get; set; } = Array.Empty<DeckDiffEntryDto>();
    public IReadOnlyList<DeckDiffEntryDto> Removed { get; set; } = Array.Empty<DeckDiffEntryDto>();
    public IReadOnlyList<DeckDiffEntryDto> Modified { get; set; } = Array.Empty<DeckDiffEntryDto>();
}

public class DeckDiffEntryDto
{
    public string ScryfallId { get; set; } = string.Empty;
    public int OldMaindeckQuantity { get; set; }
    public int OldSideboardQuantity { get; set; }
    public int NewMaindeckQuantity { get; set; }
    public int NewSideboardQuantity { get; set; }
}
