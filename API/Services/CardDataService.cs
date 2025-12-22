using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using API.Configuration;
using API.Dtos.Cards;
using API.Logging;
using API.Scryfall;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace API.Services;

public class CardDataService
{
    private readonly PathsConfig _pathsConfig;
    private readonly ScryfallConfig _scryfallConfig;
    private readonly ILogger<CardDataService> logger;

    private const string LoadOperation = "CardData.Load";

    public CardDataService(IOptions<PathsConfig> pathsConfig, IOptions<ScryfallConfig> scryfallConfig, ILogger<CardDataService> logger)
    {
        _pathsConfig = pathsConfig.Value;
        _scryfallConfig = scryfallConfig.Value;
        this.logger = logger;
    }

    public Dictionary<string, ScryfallCardDto> CardDataById { get; private set; } = new();
    public Dictionary<string, ScryfallCardDto> CardDataByName { get; private set; } = new();

    public async Task LoadCardDataAsync(CancellationToken cancellationToken = default)
    {
        using var scope = logger.BeginOperationScope(LoadOperation);
        logger.LogOperationStart(LoadOperation, new { _pathsConfig.Bulk, _scryfallConfig.BasePath });

        var stopwatch = Stopwatch.StartNew();
        var cardsById = new Dictionary<string, ScryfallCardDto>();
        var cardsByName = new Dictionary<string, ScryfallCardDto>(StringComparer.OrdinalIgnoreCase);
        var processed = 0;
        var indexed = 0;
        var skipped = 0;

        try
        {
            await foreach (var card in ScryfallUtility.FetchCardListStreamAsync(
                           _pathsConfig.Bulk,
                           _scryfallConfig.BasePath,
                           cancellationToken))
            {
                processed++;
                cancellationToken.ThrowIfCancellationRequested();

                if (!string.Equals(card.Object, "card", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(card.SetType, "memorabilia", StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(card.Id) ||
                    string.IsNullOrWhiteSpace(card.Name))
                {
                    skipped++;
                    continue;
                }

                cardsById[card.Id] = card;

                var splitNames = card.Name.Split(" // ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (splitNames.Length <= 1)
                {
                    AddOrUpdateWithCheapest(splitNames.FirstOrDefault() ?? card.Name, card);
                    indexed++;
                    continue;
                }

                foreach (var faceName in splitNames)
                {
                    if (string.IsNullOrWhiteSpace(faceName))
                    {
                        continue;
                    }

                    AddOrUpdateWithCheapest(faceName, card with { Name = faceName });
                    indexed++;
                }
            }

            CardDataById = cardsById;
            CardDataByName = cardsByName;

            stopwatch.Stop();
            logger.LogOperationSuccess(LoadOperation, new
            {
                Processed = processed,
                Skipped = skipped,
                IndexedById = cardsById.Count,
                IndexedByName = cardsByName.Count,
                DurationMs = stopwatch.Elapsed.TotalMilliseconds
            });
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            logger.LogOperationWarning(LoadOperation, "Card data loading cancelled", new { Processed = processed });
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogOperationFailure(LoadOperation, ex, new { Processed = processed });
            throw;
        }

        void AddOrUpdateWithCheapest(string key, ScryfallCardDto candidate)
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

            if (ShouldReplaceWithCheaper(candidate, existing))
            {
                cardsByName[key] = candidate;
            }
        }

        bool ShouldReplaceWithCheaper(ScryfallCardDto candidate, ScryfallCardDto existing)
        {
            var candidatePrice = GetNonFoilEuroPrice(candidate);
            var existingPrice = GetNonFoilEuroPrice(existing);

            if (candidatePrice is null && existingPrice is null)
            {
                return false;
            }

            if (candidatePrice is not null && existingPrice is null)
            {
                return true;
            }

            if (candidatePrice is null)
            {
                return false;
            }

            return candidatePrice < existingPrice;
        }
    }

    private static double? GetNonFoilEuroPrice(ScryfallCardDto card)
    {
        var priceText = card.Prices?.Eur;
        if (string.IsNullOrWhiteSpace(priceText))
        {
            return null;
        }

        return double.TryParse(priceText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    public static ImageUris ResolveImageUris(ScryfallCardDto card)
    {
        if (card.ImageUris is not null)
        {
            return card.ImageUris;
        }

        var matchingFace = card.CardFaces!
            .FirstOrDefault(face => string.Equals(face.Name, card.Name, StringComparison.OrdinalIgnoreCase))
            ?? card.CardFaces!.FirstOrDefault();

        return matchingFace?.ImageUris!;
    }

    public string? ResolveBackImageUrl(ScryfallCardDto card)
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
