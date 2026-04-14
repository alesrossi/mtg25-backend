using System.Text.Json;
using API.Dtos.Trades;
using Microsoft.Extensions.Caching.Distributed;

namespace API.Services;

public interface ITradeSessionStore
{
    Task StoreAsync(TradeConnectionDto connection, TimeSpan? timeToLive = null, CancellationToken cancellationToken = default);
    Task<TradeConnectionDto?> GetAsync(string tradeId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TradeConnectionDto>> GetAllForUserAsync(string userId, CancellationToken cancellationToken = default);
    Task DeleteAsync(string tradeId, CancellationToken cancellationToken = default);
}

public sealed class TradeSessionStore : ITradeSessionStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly IDistributedCache _cache;

    public TradeSessionStore(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task StoreAsync(TradeConnectionDto connection, TimeSpan? timeToLive = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connection.TradeId))
        {
            throw new ArgumentException(@"Trade identifier is required.", nameof(connection));
        }

        var payload = JsonSerializer.SerializeToUtf8Bytes(connection, SerializerOptions);
        var expiration = timeToLive ?? TimeSpan.FromMinutes(30);
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration
        };

        await _cache.SetAsync(GetCacheKey(connection.TradeId), payload, options, cancellationToken);
        await AddToUserIndexAsync(connection.Initiator.UserId, connection.TradeId, cancellationToken);
        await AddToUserIndexAsync(connection.Partner.UserId, connection.TradeId, cancellationToken);
    }

    public async Task<TradeConnectionDto?> GetAsync(string tradeId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tradeId))
        {
            return null;
        }

        var payload = await _cache.GetAsync(GetCacheKey(tradeId), cancellationToken);
        if (payload is null || payload.Length == 0)
        {
            return null;
        }

        return JsonSerializer.Deserialize<TradeConnectionDto>(payload, SerializerOptions);
    }

    public async Task<IReadOnlyList<TradeConnectionDto>> GetAllForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return [];
        }

        var indexPayload = await _cache.GetAsync(GetUserIndexKey(userId), cancellationToken);
        if (indexPayload is null || indexPayload.Length == 0)
        {
            return [];
        }

        var tradeIds = JsonSerializer.Deserialize<List<string>>(indexPayload, SerializerOptions) ?? [];
        var results = new List<TradeConnectionDto>(tradeIds.Count);
        var staleIds = new List<string>();

        foreach (var tradeId in tradeIds)
        {
            var trade = await GetAsync(tradeId, cancellationToken);
            if (trade is not null)
            {
                results.Add(trade);
            }
            else
            {
                staleIds.Add(tradeId);
            }
        }

        if (staleIds.Count > 0)
        {
            await RemoveFromUserIndexAsync(userId, staleIds, cancellationToken);
        }

        return results;
    }

    public async Task DeleteAsync(string tradeId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tradeId))
        {
            return;
        }

        var existing = await GetAsync(tradeId, cancellationToken);
        await _cache.RemoveAsync(GetCacheKey(tradeId), cancellationToken);

        if (existing is not null)
        {
            await RemoveFromUserIndexAsync(existing.Initiator.UserId, [tradeId], cancellationToken);
            await RemoveFromUserIndexAsync(existing.Partner.UserId, [tradeId], cancellationToken);
        }
    }

    private async Task AddToUserIndexAsync(string userId, string tradeId, CancellationToken cancellationToken)
    {
        var indexPayload = await _cache.GetAsync(GetUserIndexKey(userId), cancellationToken);
        var tradeIds = indexPayload is { Length: > 0 }
            ? JsonSerializer.Deserialize<List<string>>(indexPayload, SerializerOptions) ?? []
            : [];

        if (!tradeIds.Contains(tradeId))
        {
            tradeIds.Add(tradeId);
        }

        var updated = JsonSerializer.SerializeToUtf8Bytes(tradeIds, SerializerOptions);
        var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) };
        await _cache.SetAsync(GetUserIndexKey(userId), updated, options, cancellationToken);
    }

    private async Task RemoveFromUserIndexAsync(string userId, IEnumerable<string> tradeIdsToRemove, CancellationToken cancellationToken)
    {
        var indexPayload = await _cache.GetAsync(GetUserIndexKey(userId), cancellationToken);
        if (indexPayload is null || indexPayload.Length == 0)
        {
            return;
        }

        var tradeIds = JsonSerializer.Deserialize<List<string>>(indexPayload, SerializerOptions) ?? [];
        var removed = tradeIds.RemoveAll(id => tradeIdsToRemove.Contains(id));
        if (removed == 0)
        {
            return;
        }

        var updated = JsonSerializer.SerializeToUtf8Bytes(tradeIds, SerializerOptions);
        var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) };
        await _cache.SetAsync(GetUserIndexKey(userId), updated, options, cancellationToken);
    }

    private static string GetCacheKey(string tradeId) => $"trade-session:{tradeId}";
    private static string GetUserIndexKey(string userId) => $"user-trade-index:{userId}";
}
