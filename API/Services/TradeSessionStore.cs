using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using API.Dtos.Trades;
using Microsoft.Extensions.Caching.Distributed;

namespace API.Services;

public interface ITradeSessionStore
{
    Task StoreAsync(TradeConnectionDto connection, CancellationToken cancellationToken = default);
    Task<TradeConnectionDto?> GetAsync(string tradeId, CancellationToken cancellationToken = default);
    Task DeleteAsync(string tradeId, CancellationToken cancellationToken = default);
}

public sealed class TradeSessionStore : ITradeSessionStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly IDistributedCache cache;

    public TradeSessionStore(IDistributedCache cache)
    {
        this.cache = cache;
    }

    public async Task StoreAsync(TradeConnectionDto connection, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connection.TradeId))
        {
            throw new ArgumentException("Trade identifier is required.", nameof(connection));
        }

        var payload = JsonSerializer.SerializeToUtf8Bytes(connection, SerializerOptions);
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
        };

        await cache.SetAsync(GetCacheKey(connection.TradeId), payload, options, cancellationToken);
    }

    public async Task<TradeConnectionDto?> GetAsync(string tradeId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tradeId))
        {
            return null;
        }

        var payload = await cache.GetAsync(GetCacheKey(tradeId), cancellationToken);
        if (payload is null || payload.Length == 0)
        {
            return null;
        }

        return JsonSerializer.Deserialize<TradeConnectionDto>(payload, SerializerOptions);
    }

    public Task DeleteAsync(string tradeId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tradeId))
        {
            return Task.CompletedTask;
        }

        return cache.RemoveAsync(GetCacheKey(tradeId), cancellationToken);
    }

    private static string GetCacheKey(string tradeId) => $"trade-session:{tradeId}";
}
