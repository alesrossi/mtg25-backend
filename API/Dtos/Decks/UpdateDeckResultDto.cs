namespace API.Dtos.Decks;

public class UpdateDeckResultDto
{
    public UpdateDeckResultDto(DeckDto deck, IReadOnlyList<string> errors, int skippedLines)
    {
        Deck = deck;
        Errors = errors;
        SkippedLines = skippedLines;
    }

    public DeckDto Deck { get; }
    public IReadOnlyList<string> Errors { get; }
    public int SkippedLines { get; }
}
