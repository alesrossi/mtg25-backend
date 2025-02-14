using API.Dtos;

namespace API.Scryfall;

public class CardDataService
{
    public Dictionary<string, OracleCardDto> CardData { get; private set; } = ScryfallUtility.FetchCardListObject();

    // Initialize the object once by calling FetchCardListObject
}
