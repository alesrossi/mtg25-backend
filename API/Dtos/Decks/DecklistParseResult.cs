namespace API.Dtos.Decks;

public record DecklistParseResult(
    IReadOnlyList<CreateDeckCardDto> DeckCards,
    IReadOnlyList<string> Errors)
{
    public bool IsSuccessful => Errors.Count == 0;
}
