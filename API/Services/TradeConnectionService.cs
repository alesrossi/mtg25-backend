using API.Dtos.Notifications;
using API.Dtos.Trades;
using API.Dtos.Wishlists;
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

        var initiatorWishlists = await LoadPublicWishlistsAsync(initiator.Id);
        var partnerWishlists = await LoadPublicWishlistsAsync(partner.Id);

        var connection = new TradeConnectionDto
        {
            TradeId = Guid.NewGuid().ToString("N"),
            Initiator = CreateParticipantDto(initiator, initiatorWishlists),
            Partner = CreateParticipantDto(partner, partnerWishlists)
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

    private static TradeParticipantDto CreateParticipantDto(AppUser user, IReadOnlyList<Wishlist> wishlists)
    {
        return new TradeParticipantDto
        {
            UserId = user.Id,
            DisplayName = user.DisplayName,
            Email = user.Email ?? string.Empty,
            Wishlists = wishlists.Select(MapWishlistToSummary).ToList()
        };
    }

    private static WishlistSummaryDto MapWishlistToSummary(Wishlist wishlist)
    {
        var cards = wishlist.WishlistCards;
        return new WishlistSummaryDto
        {
            Id = wishlist.Id,
            Name = wishlist.Name,
            Description = wishlist.Description,
            IsPublic = wishlist.IsPublic,
            TotalPrice = wishlist.TotalPrice,
            TotalPriceCurrency = wishlist.TotalPriceCurrency,
            CardsCount = cards.Sum(c => c.DesiredQuantity),
            IndividualCardsCount = cards.Count
        };
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
