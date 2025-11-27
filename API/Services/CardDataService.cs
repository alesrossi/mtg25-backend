using System;
using System.Collections.Generic;
using System.Diagnostics;
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

    public Dictionary<string, OracleCardDto> CardDataById { get; private set; } = new();
    public Dictionary<string, OracleCardDto> CardDataByName { get; private set; } = new();

    public async Task LoadCardDataAsync(CancellationToken cancellationToken = default)
    {
        using var scope = logger.BeginOperationScope(LoadOperation);
        logger.LogOperationStart(LoadOperation, new { _pathsConfig.Bulk, _scryfallConfig.BasePath });

        var stopwatch = Stopwatch.StartNew();
        var cardsById = new Dictionary<string, OracleCardDto>();
        var cardsByName = new Dictionary<string, OracleCardDto>(StringComparer.OrdinalIgnoreCase);
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
                    AddOrUpdateWithOldest(splitNames.FirstOrDefault() ?? card.Name, card);
                    indexed++;
                    continue;
                }

                foreach (var faceName in splitNames)
                {
                    if (string.IsNullOrWhiteSpace(faceName))
                    {
                        continue;
                    }

                    AddOrUpdateWithOldest(faceName, card with { Name = faceName });
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

    public static ImageUris ResolveImageUris(OracleCardDto card)
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
