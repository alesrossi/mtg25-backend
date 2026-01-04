using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using API.Dtos.Binders;
using API.Dtos.Cards;
using API.Services;
using Core.Interfaces;
using Core.Models;
using Core.Models.Identity;
using static API.Helpers.CollectionValueCalculator;

namespace API.Endpoints.Binders;

public static partial class BindersEndpoint
{
    private static async Task<(IResult? Result, TradeBinder? Binder)> EnsureBinderAccessAsync(
        int binderId,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user,
        bool allowPublic = false,
        bool requireOwner = false,
        bool tracking = true)
    {
        var binder = await unitOfWork.Repository<TradeBinder>().GetByIdAsync(binderId, tracking);
        if (binder == null) return (Results.NotFound(), null);

        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var isOwner = !string.IsNullOrEmpty(userId) && binder.OwnerId == userId;

        if (requireOwner)
        {
            if (!isOwner) return (Results.Unauthorized(), null);
            return (null, binder);
        }

        if (isOwner) return (null, binder);
        if (allowPublic && binder.IsPublic) return (null, binder);

        return (Results.Unauthorized(), null);
    }

    private static BinderSummaryDto MapToSummaryDto(TradeBinder binder)
    {
        var cards = binder.BinderCards?.ToList() ?? new List<BinderCard>();

        return new BinderSummaryDto
        {
            Id = binder.Id,
            Name = binder.Name,
            Description = binder.Description,
            IsPublic = binder.IsPublic,
            CardsCount = cards.Count,
            TotalPrice = CalculateBinderTotalPrice(cards)
        };
    }

    private static BinderDto MapToDto(TradeBinder binder, IEnumerable<BinderCard>? binderCards = null)
    {
        var cards = binderCards?.ToList() ?? binder.BinderCards?.ToList() ?? new List<BinderCard>();
        var cardDtos = cards.Select(card => MapBinderCardToDto(card)).ToList();

        return new BinderDto
        {
            Id = binder.Id,
            Name = binder.Name,
            Description = binder.Description,
            IsPublic = binder.IsPublic,
            OwnerId = binder.OwnerId,
            CardsCount = cardDtos.Count,
            TotalPrice = CalculateBinderTotalPrice(cards),
            Cards = cardDtos
        };
    }

    private static BinderCardDto MapBinderCardToDto(
        BinderCard card,
        double? marketPrice = null,
        double? totalValue = null,
        MarketProvider? marketProvider = null,
        Currency? currency = null)
    {
        return new BinderCardDto
        {
            Id = card.Id,
            TradeBinderId = card.TradeBinderId,
            CardId = card.CardId,
            Card = card.Card!,
            Name = card.Name,
            QuantityToTrade = card.QuantityToTrade,
            MaxQuantityToTrade = card.QuantityToTrade,
            Notes = card.Notes,
            ImageUrl = card.Card?.ImageUrl,
            SetCode = card.Card?.SetCode,
            SetName = card.Card?.SetName,
            CollectorNumber = card.Card?.CollectorNumber,
            Rarity = card.Card?.Rarity,
            MarketPrice = marketPrice,
            TotalValue = totalValue,
            MarketProvider = marketProvider,
            Currency = currency
        };
    }

    private static double CalculateBinderTotalPrice(IEnumerable<BinderCard> cards)
    {
        var total = 0d;
        foreach (var card in cards)
        {
            var purchasePrice = card.Card?.PurchasePrice ?? 0;
            var quantity = Math.Max(0, card.QuantityToTrade);
            total += CalculateCardValue(purchasePrice, quantity);
        }

        return Math.Round(total, 2, MidpointRounding.AwayFromZero);
    }

    private static List<BinderCardDto> MapBinderCardsWithMarketData(
        IEnumerable<BinderCard> cards,
        MarketProvider preferredProvider,
        IUserSettingsService userSettingsService,
        CardDataService cardDataService)
    {
        var pricedCards = new List<BinderCardDto>();
        foreach (var card in cards)
        {
            var (marketPrice, provider) = ResolveMarketPrice(card, cardDataService, preferredProvider);
            var quantity = Math.Max(0, card.QuantityToTrade);
            var totalValue = marketPrice.HasValue
                ? Math.Round(marketPrice.Value * quantity, 2, MidpointRounding.AwayFromZero)
                : (double?)null;
            var currency = provider.HasValue
                ? userSettingsService.ResolveCurrency(provider.Value)
                : (Currency?)null;

            pricedCards.Add(MapBinderCardToDto(card, marketPrice, totalValue, provider, currency));
        }

        return pricedCards;
    }

    private static (double? Price, MarketProvider? Provider) ResolveMarketPrice(
        BinderCard card,
        CardDataService cardDataService,
        MarketProvider preferredProvider)
    {
        var marketData = TryResolveCardData(card, cardDataService);
        if (marketData?.Prices is null)
        {
            return (null, null);
        }

        var isFoil = card.Card?.IsFoil ?? false;
        foreach (var provider in EnumerateProviders(preferredProvider))
        {
            var selected = provider == MarketProvider.Mkm
                ? (isFoil ? marketData.Prices.EurFoil : marketData.Prices.Eur)
                : (isFoil ? marketData.Prices.UsdFoil : marketData.Prices.Usd);

            var parsed = TryParsePrice(selected);
            if (parsed.HasValue)
            {
                return (parsed.Value, provider);
            }
        }

        return (null, null);
    }

    private static ScryfallCardDto? TryResolveCardData(BinderCard card, CardDataService cardDataService)
    {
        if (card.Card is not null)
        {
            if (!string.IsNullOrWhiteSpace(card.Card.ScryfallId)
                && cardDataService.CardDataById.TryGetValue(card.Card.ScryfallId, out var byId))
            {
                return byId;
            }

            if (!string.IsNullOrWhiteSpace(card.Card.Name)
                && cardDataService.CardDataByName.TryGetValue(card.Card.Name, out var byName))
            {
                return byName;
            }
        }

        if (!string.IsNullOrWhiteSpace(card.Name)
            && cardDataService.CardDataByName.TryGetValue(card.Name, out var byBinderName))
        {
            return byBinderName;
        }

        return null;
    }

    private static IEnumerable<MarketProvider> EnumerateProviders(MarketProvider preferredProvider)
    {
        yield return preferredProvider;
        yield return preferredProvider == MarketProvider.Mkm ? MarketProvider.Tcg : MarketProvider.Mkm;
    }

    private static double? TryParsePrice(string? value)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }
}
