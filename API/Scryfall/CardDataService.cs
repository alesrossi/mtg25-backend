using API.Configuration;
using API.Dtos;
using Microsoft.Extensions.Options;

namespace API.Scryfall;

public class CardDataService
{
    private readonly PathsConfig _pathsConfig;
    private readonly ScryfallConfig _scryfallConfig;
    public Dictionary<string, OracleCardDto> CardDataById { get; private set; } = new();
    public Dictionary<string, OracleCardDto> CardDataByName { get; private set; } = new();
    public CardDataService(IOptions<PathsConfig> pathsConfig, IOptions<ScryfallConfig> scryfallConfig)
    {
        _pathsConfig = pathsConfig.Value;
        _scryfallConfig = scryfallConfig.Value;
    }

    public async Task LoadCardDataAsync()
    {
        // Fetch the card list asynchronously
        var cardList = await ScryfallUtility.FetchCardListObjectAsync(_pathsConfig.Bulk, _scryfallConfig.BasePath);
        
        CardDataById = (cardList).ToDictionary(x => x.Id);
        CardDataByName = (cardList).DistinctBy(x => x.Name).ToDictionary(x => x.Name, x => x);
    }

    
}
