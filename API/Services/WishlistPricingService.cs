using System.Globalization;
using Core.Interfaces;
using Core.Models;
using Core.Models.Identity;
using Core.Specifications;
using Microsoft.Extensions.Logging;

namespace API.Services;

public class WishlistPricingService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly CardDataService cardDataService;
    private readonly IUserSettingsService userSettingsService;
    private readonly ILogger<WishlistPricingService> logger;

    public WishlistPricingService(
        IUnitOfWork unitOfWork,
        CardDataService cardDataService,
        IUserSettingsService userSettingsService,
        ILogger<WishlistPricingService> logger)
    {
        this.unitOfWork = unitOfWork;
        this.cardDataService = cardDataService;
        this.userSettingsService = userSettingsService;
        this.logger = logger;
    }

    public async Task RecalculateTotalsAsync(int wishlistId)
    {
        var wishlist = await unitOfWork.Repository<Wishlist>().GetByIdAsync(wishlistId);
        if (wishlist == null)
        {
            logger.LogWarning("Wishlist {WishlistId} not found while recalculating totals", wishlistId);
            return;
        }

        var cardsSpec = new WishlistCardsWithWishlistIdSpecification(wishlistId);
        var cards = await unitOfWork.Repository<WishlistCard>().ListAsync(cardsSpec, tracking: false) ?? [];

        var marketProvider = await userSettingsService.GetMarketProviderAsync(wishlist.OwnerId);

        double total = 0;
        foreach (var card in cards)
        {
            var unitPrice = ResolveMarketPrice(card.ScryfallId, card.IsFoil ?? false, marketProvider);
            if (!unitPrice.HasValue)
            {
                continue;
            }

            var quantity = Math.Max(0, card.DesiredQuantity);
            if (quantity == 0)
            {
                continue;
            }

            total += unitPrice.Value * quantity;
        }

        wishlist.TotalPriceCurrency = total > 0 ? userSettingsService.ResolveCurrency(marketProvider) : null;
        wishlist.TotalPrice = Math.Round(total, 2, MidpointRounding.AwayFromZero);

        unitOfWork.Repository<Wishlist>().Update(wishlist);
        await unitOfWork.Complete();
    }

    private double? ResolveMarketPrice(string scryfallId, bool isFoil, MarketProvider marketProvider)
    {
        if (!cardDataService.CardDataById.TryGetValue(scryfallId, out var marketData) || marketData?.Prices is null)
        {
            return null;
        }

        var priceText = marketProvider == MarketProvider.Mkm
            ? (isFoil ? marketData.Prices.EurFoil : marketData.Prices.Eur)
            : (isFoil ? marketData.Prices.UsdFoil : marketData.Prices.Usd);

        if (string.IsNullOrWhiteSpace(priceText) && isFoil)
        {
            // Fall back to non-foil pricing when foil price is unavailable
            priceText = marketProvider == MarketProvider.Mkm
                ? marketData.Prices.Eur
                : marketData.Prices.Usd;
        }

        if (string.IsNullOrWhiteSpace(priceText))
        {
            return null;
        }

        return double.TryParse(priceText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

}
