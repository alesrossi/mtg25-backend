using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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

    public async Task LoadCardDataAsync(CancellationToken cancellationToken = default)
    {
        var cardsById = new Dictionary<string, OracleCardDto>();
        var cardsByName = new Dictionary<string, OracleCardDto>(StringComparer.OrdinalIgnoreCase);

        await foreach (var card in ScryfallUtility.FetchCardListStreamAsync(
                       _pathsConfig.Bulk,
                       _scryfallConfig.BasePath,
                       cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!string.Equals(card.Object, "card", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(card.SetType, "memorabilia", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(card.Id) || string.IsNullOrWhiteSpace(card.Name))
            {
                continue;
            }

            cardsById[card.Id] = card;

            var splitNames = card.Name.Split(" // ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (splitNames.Length <= 1)
            {
                AddOrUpdateWithOldest(splitNames.FirstOrDefault() ?? card.Name, card);
                continue;
            }

            foreach (var faceName in splitNames)
            {
                if (string.IsNullOrWhiteSpace(faceName))
                {
                    continue;
                }

                AddOrUpdateWithOldest(faceName, card with { Name = faceName });
            }
        }

        CardDataById = cardsById;
        CardDataByName = cardsByName;

        void AddOrUpdateWithOldest(string key, OracleCardDto candidate)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            if (!cardsByName.TryGetValue(key, out var existing))
            {
                cardsByName[key] = candidate;
                return;
            }

            if (IsCandidateOlder(candidate, existing))
            {
                cardsByName[key] = candidate;
            }
        }

        bool IsCandidateOlder(OracleCardDto candidate, OracleCardDto existing)
        {
            if (candidate.ReleasedAt is null)
            {
                return false;
            }

            if (existing.ReleasedAt is null)
            {
                return true;
            }

            return candidate.ReleasedAt < existing.ReleasedAt;
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

    public string? ResolveBackImageUrl(OracleCardDto card)
    {
        if (card.CardFaces is null || card.CardFaces.Count < 2)
        {
            return null;
        }

        var backFace = card.CardFaces.ElementAtOrDefault(1);
        var backImageUris = backFace?.ImageUris;
        if (backImageUris is null)
        {
            return null;
        }

        return backImageUris.Normal
            ?? backImageUris.Large
            ?? backImageUris.Png
            ?? backImageUris.Small;
    }
}
