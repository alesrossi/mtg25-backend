using API.Dtos.Binders;
using API.Dtos.Notifications;
using API.Dtos.Trades;
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
}

public sealed class TradeConnectionService : ITradeConnectionService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITradeSessionStore _sessionStore;
    private readonly NotificationService _notificationService;

    public TradeConnectionService(
        UserManager<AppUser> userManager,
        IUnitOfWork unitOfWork,
        ITradeSessionStore sessionStore,
        NotificationService notificationService)
    {
        _userManager = userManager;
        _unitOfWork = unitOfWork;
        _sessionStore = sessionStore;
        _notificationService = notificationService;
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

        var initiatorWishlistCards = ExtractWishlistCards(await LoadPublicWishlistsAsync(initiator.Id));
        var partnerWishlistCards = ExtractWishlistCards(await LoadPublicWishlistsAsync(partner.Id));

        var initiatorBinderCards = MapBinderCards(await LoadPublicBinderCardsAsync(initiator.Id));
        var partnerBinderCards = MapBinderCards(await LoadPublicBinderCardsAsync(partner.Id));

        var connection = new TradeConnectionDto
        {
            TradeId = Guid.NewGuid().ToString("N"),
            Initiator = CreateParticipantDto(initiator),
            Partner = CreateParticipantDto(partner),
            InitiatorMatches = FindMatches(initiatorBinderCards, partnerWishlistCards, initiator.Id, partner.Id),
            PartnerMatches = FindMatches(partnerBinderCards, initiatorWishlistCards, partner.Id, initiator.Id)
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

    private static IReadOnlyList<BinderCardDto> MapBinderCards(IReadOnlyList<BinderCard> cards)
    {
        return cards.Select(MapBinderCard).ToList();
    }

    private static BinderCardDto MapBinderCard(BinderCard card)
    {
        return new BinderCardDto
        {
            Id = card.Id,
            TradeBinderId = card.TradeBinderId,
            CardId = card.CardId,
            Card = card.Card!,
            Name = card.Name,
            QuantityToTrade = card.QuantityToTrade,
            Notes = card.Notes,
            ImageUrl = card.Card?.ImageUrl,
            SetCode = card.Card?.SetCode,
            SetName = card.Card?.SetName,
            CollectorNumber = card.Card?.CollectorNumber,
            Rarity = card.Card?.Rarity
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
                        CardName = binderCard.Name,
                        FromUserId = fromUserId,
                        ToUserId = toUserId,
                        OfferingCard = binderCard
                    });
                }
            }
        }

        return matches;
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
}
