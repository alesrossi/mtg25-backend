using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using API.Configuration;
using API.Dtos.Cards;
using API.Logging;
using API.Scryfall;
using Microsoft.Extensions.Options;

namespace API.Services;

public class CardDataService : ICardDataService
{
    private readonly PathsConfig _pathsConfig;
    private readonly ScryfallConfig _scryfallConfig;
    private readonly ILogger<CardDataService> _logger;

    private const string LoadOperation = "CardData.Load";

    public CardDataService(
        IOptions<PathsConfig> pathsConfig,
        IOptions<ScryfallConfig> scryfallConfig,
        ILogger<CardDataService> logger)
    {
        _pathsConfig = pathsConfig.Value;
        _scryfallConfig = scryfallConfig.Value;
        _logger = logger;
    }

    public Dictionary<string, ScryfallCardDto> CardDataById { get; private set; } = new();
    public Dictionary<string, ScryfallCardDto> CardDataByName { get; private set; } = new();

    public async Task LoadCardDataAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _logger.BeginOperationScope(LoadOperation);
        _logger.LogOperationStart(LoadOperation, new { _pathsConfig.Bulk, _scryfallConfig.BasePath });

        var stopwatch = Stopwatch.StartNew();
        var cardsById = new Dictionary<string, ScryfallCardDto>();
        var cardsByName = new Dictionary<string, ScryfallCardDto>(StringComparer.OrdinalIgnoreCase);
        var processed = 0;
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
                }
                else
                {
                    foreach (var faceName in splitNames)
                    {
                        if (string.IsNullOrWhiteSpace(faceName))
                        {
                            continue;
                        }

                        AddOrUpdateWithCheapest(faceName, card with { Name = faceName });
                    }
                }

                if (!string.IsNullOrWhiteSpace(card.FlavorName))
                {
                    AddOrUpdateWithCheapest(card.FlavorName, card);
                }

                if (!string.IsNullOrWhiteSpace(card.PrintedName))
                {
                    AddOrUpdateWithCheapest(card.PrintedName, card);
                }
            }

            CardDataById = cardsById;
            CardDataByName = cardsByName;

            stopwatch.Stop();
            _logger.LogOperationSuccess(LoadOperation, new
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
            _logger.LogOperationWarning(LoadOperation, "Card data loading cancelled", new { Processed = processed });
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogOperationFailure(LoadOperation, ex, new { Processed = processed });
            throw;
        }

        return;

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

            return candidatePrice < existingPrice;
        }
    }

    // ICardDataService — full card async lookups (wraps the in-memory dicts)

    public Task<ScryfallCardDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
        Task.FromResult(CardDataById.GetValueOrDefault(id));

    public Task<ScryfallCardDto?> GetByNameAsync(string name, CancellationToken cancellationToken = default) =>
        Task.FromResult(CardDataByName.GetValueOrDefault(name));

    public Task<List<ScryfallCardDto>> GetManyByIdAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        var result = ids
            .Select(id => CardDataById.GetValueOrDefault(id))
            .OfType<ScryfallCardDto>()
            .ToList();
        return Task.FromResult(result);
    }

    // ICardDataService — thin-index sync access (derived from in-memory dicts)

    public bool ContainsId(string id) => CardDataById.ContainsKey(id);

    public bool ContainsName(string name) => CardDataByName.ContainsKey(name);

    public bool TryGetMeta(string id, [MaybeNullWhen(false)] out CardMeta meta)
    {
        if (!CardDataById.TryGetValue(id, out var card))
        {
            meta = null;
            return false;
        }

        meta = BuildMeta(card);
        return true;
    }

    public bool TryGetIdByName(string name, [MaybeNullWhen(false)] out string id)
    {
        if (CardDataByName.TryGetValue(name, out var card))
        {
            id = card.Id;
            return true;
        }

        id = null;
        return false;
    }

    public IReadOnlyList<string> FindIdsByNameContains(string find) =>
        CardDataById
            .Where(kv =>
                kv.Value.Name.Contains(find, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(kv.Value.FlavorName) && kv.Value.FlavorName.Contains(find, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(kv.Value.PrintedName) && kv.Value.PrintedName.Contains(find, StringComparison.OrdinalIgnoreCase)))
            .Select(kv => kv.Key)
            .ToList();

    public IReadOnlyList<string> FindIdsByName(string name) =>
        CardDataById
            .Where(kv => kv.Value.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            .Select(kv => kv.Key)
            .ToList();

    public IReadOnlyList<string> FindIdsByPrinting(string name, string setCode, string collectorNumber) =>
        CardDataById
            .Where(kv =>
                string.Equals(kv.Value.Name, name, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(kv.Value.Set, setCode, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(kv.Value.CollectorNumber ?? string.Empty, collectorNumber, StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .Select(kv => kv.Key)
            .ToList();

    // Static helpers

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

    public static string? ResolveBackImageUrl(ScryfallCardDto card)
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

    public static string? ResolveOracleId(ScryfallCardDto card)
    {
        if (!string.IsNullOrWhiteSpace(card.OracleId))
        {
            return card.OracleId;
        }

        return card.CardFaces?
            .Select(face => face.OracleId)
            .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));
    }

    /// <summary>
    /// Returns Oracle card mana cost in Scryfall format (e.g. "{2}{U}{U}").
    /// For multi-face cards uses the main (first) face cost. Null or empty for lands.
    /// </summary>
    public static string? ResolveMainFaceManaCost(ScryfallCardDto? card)
    {
        if (card is null)
        {
            return null;
        }

        var cost = card.CardFaces is { Count: > 0 }
            ? card.CardFaces[0].ManaCost
            : card.ManaCost;

        return string.IsNullOrWhiteSpace(cost) ? null : cost.Trim();
    }

    private static CardMeta BuildMeta(ScryfallCardDto card) => new(
        Name: card.Name,
        OracleId: ResolveOracleId(card) ?? string.Empty,
        Set: card.Set,
        CollectorNumber: card.CollectorNumber,
        FlavorName: card.FlavorName,
        PrintedName: card.PrintedName,
        PriceUsd: card.Prices?.Usd,
        PriceUsdFoil: card.Prices?.UsdFoil,
        PriceEur: card.Prices?.Eur,
        PriceEurFoil: card.Prices?.EurFoil);

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
}
