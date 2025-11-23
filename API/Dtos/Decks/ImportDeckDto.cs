namespace API.Dtos.Decks;

public record ImportDeckDto(DeckDto Deck, List<DeckCardDto> DeckCards, IReadOnlyList<string> Errors, int SkippedLines)
{
    public override string ToString()
    {
        return $"{{ deck = {Deck}, deckCards = {DeckCards}, errors = {Errors}, skippedLines = {SkippedLines} }}";
    }
}