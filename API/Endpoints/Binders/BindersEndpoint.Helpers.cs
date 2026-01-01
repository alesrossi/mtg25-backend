using System.Security.Claims;
using API.Dtos.Binders;
using Core.Interfaces;
using Core.Models;

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
        return new BinderSummaryDto
        {
            Id = binder.Id,
            Name = binder.Name,
            Description = binder.Description,
            IsPublic = binder.IsPublic,
            CardsCount = binder.BinderCards?.Count ?? 0
        };
    }

    private static BinderDto MapToDto(TradeBinder binder, IEnumerable<BinderCard>? binderCards = null)
    {
        var cards = binderCards?.ToList() ?? binder.BinderCards?.ToList() ?? new List<BinderCard>();
        var cardDtos = cards.Select(MapToDto).ToList();

        return new BinderDto
        {
            Id = binder.Id,
            Name = binder.Name,
            Description = binder.Description,
            IsPublic = binder.IsPublic,
            OwnerId = binder.OwnerId,
            CardsCount = cardDtos.Count,
            Cards = cardDtos
        };
    }

    private static BinderCardDto MapToDto(BinderCard card)
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
            Rarity = card.Card?.Rarity
        };
    }
}
