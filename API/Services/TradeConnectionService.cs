using System;
using System.Collections.Generic;
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
using API.Constants;
using static API.Helpers.CollectionValueCalculator;

namespace API.Services;

public interface ITradeConnectionService
{
    Task<TradeConnectionDto> PrepareConnectionAsync(string initiatorUserId, string partnerUserId, bool liveTrading, CancellationToken cancellationToken = default);
    Task<TradeConnectionDto> GetConnectionAsync(string tradeId, string requesterUserId, CancellationToken cancellationToken = default);
    Task<TradeConnectionDto> UpdateConnectionAsync(string tradeId, string requesterUserId, UpdateTradeRequest request, CancellationToken cancellationToken = default);
    Task CancelConnectionAsync(string tradeId, string requesterUserId, CancellationToken cancellationToken = default);
    Task CommitTradeAsync(string tradeId, string requesterUserId, CancellationToken cancellationToken = default);
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

    public async Task<TradeConnectionDto> PrepareConnectionAsync(string initiatorUserId, string partnerUserId, bool liveTrading, CancellationToken cancellationToken = default)
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
            ValueDifference = Math.Round(initiatorTotal - partnerTotal, 2, MidpointRounding.AwayFromZero),
            IsLiveTrading = liveTrading
        };

        await _sessionStore.StoreAsync(connection, ResolveSessionDuration(connection.IsLiveTrading), cancellationToken);

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
        var requesterIsInitiator = string.Equals(connection.Initiator.UserId, requesterUserId, StringComparison.Ordinal);
        var requesterIsPartner = string.Equals(connection.Partner.UserId, requesterUserId, StringComparison.Ordinal);

        if (request.InitiatorCollectionId.HasValue)
        {
            if (!requesterIsInitiator)
            {
                throw new UnauthorizedAccessException("Only the trade initiator can choose their destination collection.");
            }

            hasChanges |= await UpdateCollectionSelectionAsync(
                connection.Initiator.UserId,
                request.InitiatorCollectionId.Value,
                () => connection.InitiatorCollectionId,
                id => connection.InitiatorCollectionId = id,
                cancellationToken);
        }

        if (request.PartnerCollectionId.HasValue)
        {
            if (!requesterIsPartner)
            {
                throw new UnauthorizedAccessException("Only the invited partner can choose their destination collection.");
            }

            hasChanges |= await UpdateCollectionSelectionAsync(
                connection.Partner.UserId,
                request.PartnerCollectionId.Value,
                () => connection.PartnerCollectionId,
                id => connection.PartnerCollectionId = id,
                cancellationToken);
        }

        if (!hasChanges)
        {
            return connection;
        }

        RecalculateTotals(connection);
        await _sessionStore.StoreAsync(connection, ResolveSessionDuration(connection.IsLiveTrading), cancellationToken);
        return connection;
    }

    public async Task CancelConnectionAsync(string tradeId, string requesterUserId, CancellationToken cancellationToken = default)
    {
        await GetConnectionAsync(tradeId, requesterUserId, cancellationToken);
        await _sessionStore.DeleteAsync(tradeId, cancellationToken);
    }

    public async Task CommitTradeAsync(string tradeId, string requesterUserId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var connection = await GetConnectionAsync(tradeId, requesterUserId, cancellationToken);

        if (!connection.IsLiveTrading)
        {
            var originPrefix = $"{connection.TradeId}.";
            var hasApproval = await _notificationService.HasApprovedNotificationAsync(
                TradeNotificationConstants.TradeCommitRequest,
                originPrefix,
                cancellationToken);
            if (!hasApproval)
            {
                throw new UnauthorizedAccessException("Trade commitment has not been approved by both participants.");
            }
        }

        var initiatorTransfers = BuildTransfers(connection.InitiatorMatches, connection.Initiator.UserId, connection.Partner.UserId);
        var partnerTransfers = BuildTransfers(connection.PartnerMatches, connection.Partner.UserId, connection.Initiator.UserId);

        if (initiatorTransfers.Count == 0 && partnerTransfers.Count == 0)
        {
            throw new InvalidOperationException("No selected cards are available to trade.");
        }

        var collectionCache = new Dictionary<string, Collection>(StringComparer.Ordinal);
        var wishlistCache = new Dictionary<string, IReadOnlyList<Wishlist>>(StringComparer.Ordinal);
        var wishlistsToRecalculate = new HashSet<int>();
        var recipientSelections = new Dictionary<string, int?>(StringComparer.Ordinal)
        {
            [connection.Initiator.UserId] = connection.InitiatorCollectionId,
            [connection.Partner.UserId] = connection.PartnerCollectionId
        };

        EnsureRecipientCollectionsSelected(initiatorTransfers, partnerTransfers, recipientSelections);

        foreach (var transfer in initiatorTransfers.Concat(partnerTransfers))
        {
            var recipientCollectionId = recipientSelections.TryGetValue(transfer.ToUserId, out var collectionId)
                ? collectionId
                : null;

            await ExecuteTransferAsync(
                transfer,
                collectionCache,
                wishlistCache,
                wishlistsToRecalculate,
                recipientCollectionId,
                cancellationToken);
        }

        foreach (var wishlistId in wishlistsToRecalculate)
        {
            await RecalculateWishlistTotalsAsync(wishlistId, cancellationToken);
        }

        await _unitOfWork.Complete();
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

    private static TimeSpan ResolveSessionDuration(bool isLiveTrading) =>
        isLiveTrading ? TimeSpan.FromMinutes(30) : TimeSpan.FromHours(12);

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

    private async Task<bool> UpdateCollectionSelectionAsync(
        string participantUserId,
        int requestedCollectionId,
        Func<int?> getCurrentValue,
        Action<int?> setCurrentValue,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (requestedCollectionId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(requestedCollectionId), "Collection identifier must be positive.");
        }

        var repository = _unitOfWork.Repository<Collection>();
        var collection = await repository.GetByIdAsync(requestedCollectionId);
        if (collection is null || !string.Equals(collection.OwnerId, participantUserId, StringComparison.Ordinal))
        {
            throw new ArgumentException("Collection not found or not owned by the participant.");
        }

        if (getCurrentValue() == collection.Id)
        {
            return false;
        }

        setCurrentValue(collection.Id);
        return true;
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

    private static IReadOnlyList<TradeTransfer> BuildTransfers(IEnumerable<TradeMatchDto> matches, string fromUserId, string toUserId)
    {
        return matches
            .Where(match => match.IsSelected && match.OfferingCard.QuantityToTrade > 0)
            .GroupBy(match => match.OfferingCard.Id)
            .Select(group =>
            {
                var reference = group.First();
                var quantity = Math.Max(0, Math.Min(reference.OfferingCard.QuantityToTrade, reference.OfferingCard.MaxQuantityToTrade));
                return new TradeTransfer(
                    reference.OfferingCard.Id,
                    reference.OfferingCard.CardId,
                    reference.OfferingCard.TradeBinderId,
                    reference.CardName,
                    quantity,
                    fromUserId,
                    toUserId,
                    reference.OfferingCard.MarketPrice,
                    reference.OfferingCard.Currency);
            })
            .Where(transfer => transfer.Quantity > 0)
            .ToList();
    }

    private static void EnsureRecipientCollectionsSelected(
        IEnumerable<TradeTransfer> initiatorTransfers,
        IEnumerable<TradeTransfer> partnerTransfers,
        IReadOnlyDictionary<string, int?> selections)
    {
        var recipients = initiatorTransfers
            .Concat(partnerTransfers)
            .Select(transfer => transfer.ToUserId)
            .Distinct(StringComparer.Ordinal);

        foreach (var recipient in recipients)
        {
            if (!selections.TryGetValue(recipient, out var collectionId) || !collectionId.HasValue)
            {
                throw new InvalidOperationException("Both participants must select a destination collection before committing the trade.");
            }
        }
    }

    private async Task ExecuteTransferAsync(
        TradeTransfer transfer,
        IDictionary<string, Collection> collectionCache,
        IDictionary<string, IReadOnlyList<Wishlist>> wishlistCache,
        ISet<int> wishlistsToRecalculate,
        int? recipientCollectionId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var binderCard = await _unitOfWork.Repository<BinderCard>()
            .GetEntityWithSpec(new BinderCardWithBinderSpecification(transfer.BinderCardId));

        if (binderCard is null)
        {
            throw new KeyNotFoundException($"Binder card '{transfer.BinderCardId}' was not found.");
        }

        if (!string.Equals(binderCard.TradeBinder.OwnerId, transfer.FromUserId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Trade data is no longer valid for one of the participants.");
        }

        if (binderCard.QuantityToTrade < transfer.Quantity)
        {
            throw new InvalidOperationException($"Not enough quantity available for binder card '{transfer.BinderCardId}'.");
        }

        var card = binderCard.Card ?? await _unitOfWork.Repository<Card>().GetByIdAsync(binderCard.CardId);
        if (card is null)
        {
            throw new InvalidOperationException($"Card '{transfer.CardId}' was not found.");
        }

        var giverCollection = card.Collection ?? await _unitOfWork.Repository<Collection>().GetByIdAsync(card.CollectionId);
        if (giverCollection is null || !string.Equals(giverCollection.OwnerId, transfer.FromUserId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Collection ownership mismatch while committing trade.");
        }

        if (card.Quantity < transfer.Quantity)
        {
            throw new InvalidOperationException($"Not enough copies of '{card.Name}' to complete the trade.");
        }

        var removedValue = CalculateCardValue(card.PurchasePrice, transfer.Quantity);
        card.Quantity -= transfer.Quantity;
        giverCollection.NumberOfCards = Math.Max(0, giverCollection.NumberOfCards - transfer.Quantity);
        giverCollection.TotalPrice = ApplyTotalPriceDelta(giverCollection.TotalPrice, -removedValue);

        if (card.Quantity <= 0)
        {
            _unitOfWork.Repository<Card>().Delete(card);
        }
        else
        {
            _unitOfWork.Repository<Card>().Update(card);
        }

        _unitOfWork.Repository<Collection>().Update(giverCollection);
        _unitOfWork.Repository<BinderCard>().Delete(binderCard);

        var recipientCollection = await ResolveRecipientCollectionAsync(
            transfer.ToUserId,
            recipientCollectionId,
            collectionCache,
            cancellationToken);
        await AddCardToCollectionAsync(
            recipientCollection,
            card,
            transfer.Quantity,
            transfer.MarketPrice,
            transfer.Currency);

        await RemoveWishlistEntriesAsync(
            transfer.ToUserId,
            transfer.CardName,
            transfer.Quantity,
            wishlistCache,
            wishlistsToRecalculate,
            cancellationToken);
    }

    private async Task<Collection> ResolveRecipientCollectionAsync(
        string userId,
        int? requestedCollectionId,
        IDictionary<string, Collection> cache,
        CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(userId, out var cached)
            && (!requestedCollectionId.HasValue || cached.Id == requestedCollectionId.Value))
        {
            return cached;
        }

        if (!requestedCollectionId.HasValue)
        {
            throw new InvalidOperationException("Destination collection must be provided by each participant before committing the trade.");
        }

        var repository = _unitOfWork.Repository<Collection>();
        var collection = await repository.GetByIdAsync(requestedCollectionId.Value);
        if (collection is null || !string.Equals(collection.OwnerId, userId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The selected collection is no longer available.");
        }

        cache[userId] = collection;
        return collection;
    }

    private Task AddCardToCollectionAsync(
        Collection collection,
        Card sourceCard,
        int quantity,
        double? livePrice,
        Currency? liveCurrency)
    {
        var isNewCollection = collection.Id == 0;
        var resolvedPrice = livePrice ?? sourceCard.PurchasePrice;
        var resolvedCurrency = ResolvePurchaseCurrency(liveCurrency, sourceCard.PurchasePriceCurrency);

        var receivedCard = new Card
        {
            Name = sourceCard.Name,
            ScryfallId = sourceCard.ScryfallId,
            Collection = collection,
            CollectionId = collection.Id,
            Quantity = quantity,
            Language = sourceCard.Language,
            Condition = sourceCard.Condition,
            IsFoil = sourceCard.IsFoil,
            PurchasePrice = resolvedPrice,
            PurchasePriceCurrency = resolvedCurrency,
            ImageUrl = sourceCard.ImageUrl,
            BackImageUrl = sourceCard.BackImageUrl,
            ArtCrop = sourceCard.ArtCrop,
            SetCode = sourceCard.SetCode,
            SetName = sourceCard.SetName,
            TypeLine = sourceCard.TypeLine,
            CollectorNumber = sourceCard.CollectorNumber,
            Rarity = sourceCard.Rarity,
            IsMisprint = sourceCard.IsMisprint,
            IsAltered = sourceCard.IsAltered
        };

        _unitOfWork.Repository<Card>().Add(receivedCard);
        collection.NumberOfCards += quantity;
        var addedValue = CalculateCardValue(receivedCard.PurchasePrice, receivedCard.Quantity);
        collection.TotalPrice = ApplyTotalPriceDelta(collection.TotalPrice, addedValue);

        if (!isNewCollection)
        {
            _unitOfWork.Repository<Collection>().Update(collection);
        }

        return Task.CompletedTask;
    }

    private static string ResolvePurchaseCurrency(Currency? currency, string fallbackCurrency)
    {
        if (currency.HasValue)
        {
            return ConvertCurrencyToCode(currency.Value);
        }

        return string.IsNullOrWhiteSpace(fallbackCurrency)
            ? "USD"
            : fallbackCurrency;
    }

    private static string ConvertCurrencyToCode(Currency currency)
    {
        return currency.ToString().ToUpperInvariant();
    }

    private async Task<IReadOnlyList<Wishlist>> GetWishlistsForUserAsync(
        string ownerId,
        IDictionary<string, IReadOnlyList<Wishlist>> cache,
        CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(ownerId, out var cached))
        {
            return cached;
        }

        var spec = new WishlistsWithOwnerSpecification(ownerId, includeCards: true);
        var wishlists = await _unitOfWork.Repository<Wishlist>().ListAsync(spec) ?? new List<Wishlist>();
        cache[ownerId] = wishlists;
        return wishlists;
    }

    private async Task RemoveWishlistEntriesAsync(
        string ownerId,
        string cardName,
        int quantity,
        IDictionary<string, IReadOnlyList<Wishlist>> wishlistCache,
        ISet<int> wishlistsToRecalculate,
        CancellationToken cancellationToken)
    {
        if (quantity <= 0)
        {
            return;
        }

        var wishlists = await GetWishlistsForUserAsync(ownerId, wishlistCache, cancellationToken);
        var remaining = quantity;

        foreach (var wishlist in wishlists)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (wishlist.WishlistCards is null || wishlist.WishlistCards.Count == 0)
            {
                continue;
            }

            var matchingCards = wishlist.WishlistCards
                .Where(card => string.Equals(card.Name, cardName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matchingCards.Count == 0)
            {
                continue;
            }

            foreach (var wishlistCard in matchingCards)
            {
                if (remaining <= 0)
                {
                    break;
                }

                var desiredQuantity = Math.Max(0, wishlistCard.DesiredQuantity);
                if (desiredQuantity <= remaining)
                {
                    remaining -= desiredQuantity == 0 ? 1 : desiredQuantity;
                    _unitOfWork.Repository<WishlistCard>().Delete(wishlistCard);
                    wishlist.WishlistCards.Remove(wishlistCard);
                }
                else
                {
                    wishlistCard.DesiredQuantity = desiredQuantity - remaining;
                    _unitOfWork.Repository<WishlistCard>().Update(wishlistCard);
                    remaining = 0;
                }

                wishlistsToRecalculate.Add(wishlist.Id);
            }

            if (remaining <= 0)
            {
                break;
            }
        }
    }

    private async Task RecalculateWishlistTotalsAsync(int wishlistId, CancellationToken cancellationToken)
    {
        var wishlist = await _unitOfWork.Repository<Wishlist>().GetByIdAsync(wishlistId);
        if (wishlist is null)
        {
            return;
        }

        var cardsSpec = new WishlistCardsWithWishlistIdSpecification(wishlistId);
        var cards = await _unitOfWork.Repository<WishlistCard>().ListAsync(cardsSpec, tracking: false) ?? Array.Empty<WishlistCard>();

        var marketProvider = await _userSettingsService.GetMarketProviderAsync(wishlist.OwnerId, cancellationToken);
        var currency = _userSettingsService.ResolveCurrency(marketProvider);

        double total = 0;
        foreach (var wishlistCard in cards)
        {
            var price = ResolveWishlistMarketPrice(wishlistCard.ScryfallId, wishlistCard.IsFoil ?? false, marketProvider);
            if (!price.HasValue)
            {
                continue;
            }

            var desiredQuantity = Math.Max(0, wishlistCard.DesiredQuantity);
            if (desiredQuantity == 0)
            {
                continue;
            }

            total += price.Value * desiredQuantity;
        }

        wishlist.TotalPrice = Math.Round(total, 2, MidpointRounding.AwayFromZero);
        wishlist.TotalPriceCurrency = total > 0 ? currency : null;

        _unitOfWork.Repository<Wishlist>().Update(wishlist);
    }

    private double? ResolveWishlistMarketPrice(string scryfallId, bool isFoil, MarketProvider marketProvider)
    {
        if (!_cardDataService.CardDataById.TryGetValue(scryfallId, out var marketData) || marketData?.Prices is null)
        {
            return null;
        }

        var priceText = marketProvider == MarketProvider.Mkm
            ? (isFoil ? marketData.Prices.EurFoil : marketData.Prices.Eur)
            : (isFoil ? marketData.Prices.UsdFoil : marketData.Prices.Usd);

        if (string.IsNullOrWhiteSpace(priceText) && isFoil)
        {
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

    private sealed record TradeTransfer(
        int BinderCardId,
        int CardId,
        int TradeBinderId,
        string CardName,
        int Quantity,
        string FromUserId,
        string ToUserId,
        double? MarketPrice,
        Currency? Currency);
}
