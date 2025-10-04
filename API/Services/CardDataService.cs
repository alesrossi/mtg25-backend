using System;
using System.Collections.Generic;
using System.Linq;
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

        var cardsByName = new Dictionary<string, OracleCardDto>(StringComparer.OrdinalIgnoreCase);

        foreach (var card in cardList)
        {
            var splitNames = card.Name.Split(" // ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (splitNames.Length <= 1)
            {
                AddIfMissing(splitNames.FirstOrDefault() ?? card.Name, card);
                continue;
            }

            foreach (var faceName in splitNames)
            {
                if (string.IsNullOrWhiteSpace(faceName))
                {
                    continue;
                }

                AddIfMissing(faceName, card with { Name = faceName });
            }
        }

        CardDataByName = cardsByName;

        void AddIfMissing(string key, OracleCardDto value)
        {
            if (!cardsByName.ContainsKey(key))
            {
                cardsByName[key] = value;
            }
        }
    }

    public ImageUris? ResolveImageUris(OracleCardDto card)
    {
        if (card.ImageUris is not null)
        {
            return card.ImageUris;
        }

        if (card.CardFaces is null || card.CardFaces.Count == 0)
        {
            return null;
        }

        var matchingFace = card.CardFaces
            .FirstOrDefault(face => string.Equals(face.Name, card.Name, StringComparison.OrdinalIgnoreCase))
            ?? card.CardFaces.FirstOrDefault();

        return matchingFace?.ImageUris;
    }
}
