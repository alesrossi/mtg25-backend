using System.Diagnostics.CodeAnalysis;
using API.Dtos.Cards;

namespace API.Services;

public record CardMeta(
    string Name,
    string OracleId,
    string Set,
    string? CollectorNumber,
    string? FlavorName,
    string? PrintedName,
    string? PriceUsd,
    string? PriceUsdFoil,
    string? PriceEur,
    string? PriceEurFoil);

public interface ICardDataService
{
    Task LoadCardDataAsync(CancellationToken cancellationToken = default);

    // Full card lookups — go to Redis when UseRedisCache is enabled
    Task<ScryfallCardDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<ScryfallCardDto?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<List<ScryfallCardDto>> GetManyByIdAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default);

    // Thin-index access — always synchronous (in-memory in both impls)
    bool ContainsId(string id);
    bool ContainsName(string name);
    bool TryGetMeta(string id, [MaybeNullWhen(false)] out CardMeta meta);
    bool TryGetIdByName(string name, [MaybeNullWhen(false)] out string id);

    // In-memory search helpers
    IReadOnlyList<string> FindIdsByNameContains(string find);
    IReadOnlyList<string> FindIdsByName(string name);
    IReadOnlyList<string> FindIdsByPrinting(string name, string setCode, string collectorNumber);
}
