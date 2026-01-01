using System.Linq;
using API.Dtos.Binders;
using API.Dtos.Notifications;
using API.Dtos.Trades;
using API.Dtos.Cards;
using System.Globalization;
using Core.Interfaces;
using Core.Models;
using Core.Models.Identity;
using Core.Specifications;
using Microsoft.AspNetCore.Identity;

namespace API.Services;

public interface ITradeConnectionService
{
    Task<TradeConnectionDto> PrepareConnectionAsync(string initiatorUserId, string partnerUserId, CancellationToken cancellationToken = default);
    Task<TradeConnectionDto> GetConnectionAsync(string tradeId, string requesterUserId, CancellationToken cancellationToken = default);
    Task<TradeConnectionDto> UpdateConnectionAsync(string tradeId, string requesterUserId, UpdateTradeRequest request, CancellationToken cancellationToken = default);
    Task CancelConnectionAsync(string tradeId, string requesterUserId, CancellationToken cancellationToken = default);
}

public sealed class TradeConnectionService : ITradeConnectionService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITradeSessionStore _sessionStore;
    private readonly NotificationService _notificationService;
    private readonly CardDataService _cardDataService;
    private readonly IUserSettingsService _userSettingsService;

    public TradeConnectionService(
        UserManager<AppUser> userManager,
        IUnitOfWork unitOfWork,
        ITradeSessionStore sessionStore,
        NotificationService notificationService,
        CardDataService cardDataService,
        IUserSettingsService userSettingsService)
    {
        _userManager = userManager;
        _unitOfWork = unitOfWork;
        _sessionStore = sessionStore;
        _notificationService = notificationService;
        _cardDataService = cardDataService;
        _userSettingsService = userSettingsService;
    }

    public async Task<TradeConnectionDto> PrepareConnectionAsync(string initiatorUserId, string partnerUserId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(initiatorUserId) || string.IsNullOrWhiteSpace(partnerUserId))
        {
            throw new ArgumentException("Both user identifiers are required.");
        }

        if (string.Equals(initiatorUserId, partnerUserId, StringComparison.Ordinal))
        {
            throw new ArgumentException("Cannot start a trade with the same user.");
        }

        var initiator = await _userManager.FindByIdAsync(initiatorUserId);
        if (initiator is null)
        {
            throw new KeyNotFoundException($"User '{initiatorUserId}' was not found.");
        }

        var partner = await _userManager.FindByIdAsync(partnerUserId);
        if (partner is null)
        {
            throw new KeyNotFoundException($"User '{partnerUserId}' was not found.");
        }

        var priceProvider = await _userSettingsService.GetMarketProviderAsync(initiator.Id, cancellationToken);
        var priceCurrency = _userSettingsService.ResolveCurrency(priceProvider);

        var initiatorWishlistCards = ExtractWishlistCards(await LoadPublicWishlistsAsync(initiator.Id));
        var partnerWishlistCards = ExtractWishlistCards(await LoadPublicWishlistsAsync(partner.Id));

        var initiatorBinderCards = MapBinderCards(await LoadPublicBinderCardsAsync(initiator.Id), priceProvider);
        var partnerBinderCards = MapBinderCards(await LoadPublicBinderCardsAsync(partner.Id), priceProvider);

        var initiatorMatches = FindMatches(initiatorBinderCards, partnerWishlistCards, initiator.Id, partner.Id);
        var partnerMatches = FindMatches(partnerBinderCards, initiatorWishlistCards, partner.Id, initiator.Id);

        var initiatorTotal = CalculateTotalValue(initiatorMatches);
        var partnerTotal = CalculateTotalValue(partnerMatches);

        var connection = new TradeConnectionDto
        {
            TradeId = Guid.NewGuid().ToString("N"),
            Initiator = CreateParticipantDto(initiator),
            Partner = CreateParticipantDto(partner),
            InitiatorMatches = initiatorMatches,
            PartnerMatches = partnerMatches,
            PriceProvider = priceProvider,
            PriceCurrency = priceCurrency,
            InitiatorTotalValue = initiatorTotal,
            PartnerTotalValue = partnerTotal,
            ValueDifference = Math.Round(initiatorTotal - partnerTotal, 2, MidpointRounding.AwayFromZero)
        };

        await _sessionStore.StoreAsync(connection, cancellationToken);
        await NotifyParticipantsAsync(connection, cancellationToken);

        return connection;
    }

    public async Task<TradeConnectionDto> GetConnectionAsync(string tradeId, string requesterUserId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(tradeId))
        {
            throw new ArgumentException("Trade identifier is required.", nameof(tradeId));
        }

        if (string.IsNullOrWhiteSpace(requesterUserId))
        {
            throw new ArgumentException("User identifier is required.", nameof(requesterUserId));
        }

        var connection = await _sessionStore.GetAsync(tradeId, cancellationToken);
        if (connection is null)
        {
            throw new KeyNotFoundException($"Trade session '{tradeId}' was not found.");
        }

        var isParticipant = string.Equals(connection.Initiator.UserId, requesterUserId, StringComparison.Ordinal)
                            || string.Equals(connection.Partner.UserId, requesterUserId, StringComparison.Ordinal);

        return !isParticipant ? throw new UnauthorizedAccessException("User is not part of this trade session.") : connection;
    }

    public async Task<TradeConnectionDto> UpdateConnectionAsync(
        string tradeId,
        string requesterUserId,
        UpdateTradeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var connection = await GetConnectionAsync(tradeId, requesterUserId, cancellationToken);

        var hasChanges = false;
        hasChanges |= ApplyMatchUpdates(connection.InitiatorMatches, request.InitiatorMatches);
        hasChanges |= ApplyMatchUpdates(connection.PartnerMatches, request.PartnerMatches);

        if (!hasChanges)
        {
            return connection;
        }

        RecalculateTotals(connection);
        await _sessionStore.StoreAsync(connection, cancellationToken);
        return connection;
    }

    public async Task CancelConnectionAsync(string tradeId, string requesterUserId, CancellationToken cancellationToken = default)
    {
        await GetConnectionAsync(tradeId, requesterUserId, cancellationToken);
        await _sessionStore.DeleteAsync(tradeId, cancellationToken);
    }

    private async Task<IReadOnlyList<Wishlist>> LoadPublicWishlistsAsync(string ownerId)
    {
        var spec = new PublicWishlistsForOwnerSpecification(ownerId, includeCards: true);
        return await _unitOfWork.Repository<Wishlist>().ListAsync(spec, tracking: false) ?? [];
    }

    private async Task<IReadOnlyList<BinderCard>> LoadPublicBinderCardsAsync(string ownerId)
    {
        var spec = new PublicBinderCardsForOwnerSpecification(ownerId);
        return await _unitOfWork.Repository<BinderCard>().ListAsync(spec, tracking: false) ?? [];
    }

    private static TradeParticipantDto CreateParticipantDto(AppUser user)
    {
        return new TradeParticipantDto
        {
            UserId = user.Id,
            DisplayName = user.DisplayName,
            Email = user.Email ?? string.Empty
        };
    }

    private static IReadOnlyList<WishlistCard> ExtractWishlistCards(IReadOnlyList<Wishlist> wishlists)
    {
        return wishlists
            .SelectMany(w => w.WishlistCards ?? Array.Empty<WishlistCard>())
            .ToList();
    }

    private IReadOnlyList<BinderCardDto> MapBinderCards(IReadOnlyList<BinderCard> cards, MarketProvider preferredProvider)
    {
        return cards.Select(card => MapBinderCard(card, preferredProvider)).ToList();
    }

    private BinderCardDto MapBinderCard(BinderCard card, MarketProvider preferredProvider)
    {
        var (marketPrice, marketProvider) = ResolveMarketPrice(card, preferredProvider);
        var quantity = Math.Max(0, card.QuantityToTrade);
        var totalValue = marketPrice.HasValue
            ? Math.Round(marketPrice.Value * quantity, 2, MidpointRounding.AwayFromZero)
            : (double?)null;
        var currency = marketProvider.HasValue
            ? _userSettingsService.ResolveCurrency(marketProvider.Value)
            : (Currency?)null;

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

    private static IReadOnlyList<TradeMatchDto> FindMatches(
        IEnumerable<BinderCardDto> offeringCards,
        IEnumerable<WishlistCard> desiredCards,
        string fromUserId,
        string toUserId)
    {
        var desiredLookup = desiredCards
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var matches = new List<TradeMatchDto>();
        foreach (var binderCard in offeringCards)
        {
            if (desiredLookup.TryGetValue(binderCard.Name, out var wishlistCards))
            {
                foreach (var wishlistCard in wishlistCards)
                {
                    matches.Add(new TradeMatchDto
                    {
                        MatchId = Guid.NewGuid().ToString("N"),
                        CardName = binderCard.Name,
                        FromUserId = fromUserId,
                        ToUserId = toUserId,
                        OfferingCard = binderCard,
                        IsSelected = true
                    });
                }
            }
        }

        return matches;
    }

    private static double CalculateTotalValue(IEnumerable<TradeMatchDto> matches)
    {
        return matches
            .Where(match => match.IsSelected)
            .Sum(match => match.OfferingCard.TotalValue ?? 0d);
    }

    private static void RecalculateTotals(TradeConnectionDto connection)
    {
        connection.InitiatorTotalValue = CalculateTotalValue(connection.InitiatorMatches);
        connection.PartnerTotalValue = CalculateTotalValue(connection.PartnerMatches);
        connection.ValueDifference = Math.Round(connection.InitiatorTotalValue - connection.PartnerTotalValue, 2, MidpointRounding.AwayFromZero);
    }

    private (double? Price, MarketProvider? Provider) ResolveMarketPrice(BinderCard card, MarketProvider preferredProvider)
    {
        var marketData = TryResolveCardData(card);
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

    private ScryfallCardDto? TryResolveCardData(BinderCard card)
    {
        if (card.Card is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(card.Card.ScryfallId)
            && _cardDataService.CardDataById.TryGetValue(card.Card.ScryfallId, out var byId))
        {
            return byId;
        }

        if (!string.IsNullOrWhiteSpace(card.Card.Name)
            && _cardDataService.CardDataByName.TryGetValue(card.Card.Name, out var byName))
        {
            return byName;
        }

        if (!string.IsNullOrWhiteSpace(card.Name)
            && _cardDataService.CardDataByName.TryGetValue(card.Name, out var byBinderName))
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

    private static bool ApplyMatchUpdates(IReadOnlyList<TradeMatchDto> matches, IReadOnlyList<TradeMatchUpdateDto>? updates)
    {
        if (updates is null || updates.Count == 0)
        {
            return false;
        }

        var ensureIdentifiersChanged = EnsureMatchIdentifiers(matches);
        var matchLookup = matches.ToDictionary(m => m.MatchId, StringComparer.Ordinal);
        var hasChanges = ensureIdentifiersChanged;

        foreach (var update in updates)
        {
            if (string.IsNullOrWhiteSpace(update.MatchId))
            {
                throw new ArgumentException("Match identifier is required.", nameof(updates));
            }

            if (!matchLookup.TryGetValue(update.MatchId, out var match))
            {
                throw new KeyNotFoundException($"Match '{update.MatchId}' was not found in this trade.");
            }

            if (update.QuantityToTrade.HasValue)
            {
                var quantity = update.QuantityToTrade.Value;
                if (quantity < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(update.QuantityToTrade), "Quantity cannot be negative.");
                }

                var maxQuantity = Math.Max(0, match.OfferingCard.MaxQuantityToTrade);
                if (quantity > maxQuantity)
                {
                    throw new ArgumentException($"Quantity cannot exceed {maxQuantity} for match '{update.MatchId}'.");
                }

                if (match.OfferingCard.QuantityToTrade != quantity)
                {
                    match.OfferingCard.QuantityToTrade = quantity;
                    UpdateCardTotals(match.OfferingCard);
                    hasChanges = true;
                }
            }

            if (update.IsSelected.HasValue && match.IsSelected != update.IsSelected.Value)
            {
                match.IsSelected = update.IsSelected.Value;
                hasChanges = true;
            }
        }

        return hasChanges;
    }

    private async Task NotifyParticipantsAsync(TradeConnectionDto connection, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var partnerNotification = new NewNotificationDto
        {
            Name = "trade_session",
            Message = $"{connection.Initiator.DisplayName} wants to trade with you.",
            Origin = $"{connection.TradeId}.{connection.Initiator.UserId}",
            ObjectId = connection.TradeId,
            AppUserId = connection.Partner.UserId
        };
        
        await _notificationService.CreateNotificationAsync(partnerNotification);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void UpdateCardTotals(BinderCardDto card)
    {
        if (card.MarketPrice.HasValue)
        {
            card.TotalValue = Math.Round(card.MarketPrice.Value * card.QuantityToTrade, 2, MidpointRounding.AwayFromZero);
        }
        else
        {
            card.TotalValue = null;
        }
    }

    private static bool EnsureMatchIdentifiers(IEnumerable<TradeMatchDto> matches)
    {
        var hasChanges = false;
        foreach (var match in matches)
        {
            if (string.IsNullOrWhiteSpace(match.MatchId))
            {
                match.MatchId = Guid.NewGuid().ToString("N");
                hasChanges = true;
            }
        }

        return hasChanges;
    }
}
