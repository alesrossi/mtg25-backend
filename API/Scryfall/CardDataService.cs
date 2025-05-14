using API.Configuration;
using API.Dtos;
using Microsoft.Extensions.Options;

namespace API.Scryfall;

public class CardDataService
{
    private readonly PathsConfig _pathsConfig;
    private readonly ScryfallConfig _scryfallConfig;
    public Dictionary<string, OracleCardDto> CardDataById { get; private set; } = new();
    public CardDataService(IOptions<PathsConfig> pathsConfig, IOptions<ScryfallConfig> scryfallConfig)
    {
        _pathsConfig = pathsConfig.Value;
        _scryfallConfig = scryfallConfig.Value;
    }

    public async Task LoadCardDataAsync()
    {
        // Fetch the card dictionary asynchronously
        CardDataById = (await ScryfallUtility.FetchCardListObjectAsync(_pathsConfig.Bulk, _scryfallConfig.BasePath)).ToDictionary(x => x.OracleId);
    }

    
}
