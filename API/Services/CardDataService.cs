using System;
using API.Configuration;
using API.Dtos.Cards;
using API.Scryfall;
using Microsoft.Extensions.Options;

namespace API.Services;

public class CardDataService(IOptions<PathsConfig> pathsConfig, IOptions<ScryfallConfig> scryfallConfig)
{
    private readonly PathsConfig _pathsConfig = pathsConfig.Value;
    private readonly ScryfallConfig _scryfallConfig = scryfallConfig.Value;
    public Dictionary<string, OracleCardDto> CardDataById { get; private set; } = new();
    public Dictionary<string, OracleCardDto> CardDataByName { get; private set; } = new();

    public async Task LoadCardDataAsync()
    {
        // Fetch the card list asynchronously
        var cardList = await ScryfallUtility.FetchCardListObjectAsync(_pathsConfig.Bulk, _scryfallConfig.BasePath);
        
        CardDataById = cardList.ToDictionary(x => x.Id);
        CardDataByName = cardList
            .DistinctBy(x => x.Name)
            .ToDictionary(x => x.Name, x => x, StringComparer.OrdinalIgnoreCase);
    }

    
}
