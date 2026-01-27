using Core.Models.Identity;
using Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Core.Enums;

namespace API.Services;

public interface IUserSettingsService
{
    Task<Settings?> GetSettingsAsync(string userId, CancellationToken cancellationToken = default);
    Task<MarketProvider> GetMarketProviderAsync(string userId, CancellationToken cancellationToken = default);
    Task<Currency> GetCurrencyAsync(string userId, CancellationToken cancellationToken = default);
    MarketProvider ResolveMarketProvider(MarketProvider? storedProvider);
    Currency ResolveCurrency(MarketProvider marketProvider);
}

public class UserSettingsService : IUserSettingsService
{
    private readonly AppIdentityDbContext _identityDbContext;

    public UserSettingsService(AppIdentityDbContext identityDbContext)
    {
        _identityDbContext = identityDbContext;
    }

    public async Task<Settings?> GetSettingsAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException(@"User identifier is required", nameof(userId));
        }

        return await _identityDbContext.Settings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.AppUserId == userId, cancellationToken);
    }

    public async Task<MarketProvider> GetMarketProviderAsync(string userId, CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(userId, cancellationToken);
        return ResolveMarketProvider(settings?.MarketProvider);
    }

    public async Task<Currency> GetCurrencyAsync(string userId, CancellationToken cancellationToken = default)
    {
        var provider = await GetMarketProviderAsync(userId, cancellationToken);
        return ResolveCurrency(provider);
    }

    public MarketProvider ResolveMarketProvider(MarketProvider? storedProvider)
    {
        return storedProvider ?? MarketProvider.Mkm;
    }

    public Currency ResolveCurrency(MarketProvider marketProvider)
    {
        return marketProvider == MarketProvider.Mkm ? Currency.Eur : Currency.Usd;
    }
}