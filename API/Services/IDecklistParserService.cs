using System.Collections.Generic;
using System.Threading.Tasks;
using API.Dtos.Decks;

namespace API.Services;

public interface IDecklistParserService
{
    Task<DecklistParseResult> ParseAsync(IEnumerable<string> decklistLines);
}
