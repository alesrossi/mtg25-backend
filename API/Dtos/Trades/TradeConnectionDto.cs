using System;
using System.Collections.Generic;
using API.Dtos.Binders;
using Core.Models.Identity;

namespace API.Dtos.Trades;

public sealed class TradeConnectionDto
{
    public string TradeId { get; init; } = string.Empty;
    public TradeParticipantDto Initiator { get; init; } = new();
    public TradeParticipantDto Partner { get; init; } = new();
    public IReadOnlyList<TradeMatchDto> InitiatorMatches { get; init; } = Array.Empty<TradeMatchDto>();
    public IReadOnlyList<TradeMatchDto> PartnerMatches { get; init; } = Array.Empty<TradeMatchDto>();
    public MarketProvider PriceProvider { get; init; } = MarketProvider.Mkm;
    public Currency PriceCurrency { get; init; } = Currency.Eur;
    public double InitiatorTotalValue { get; init; }
    public double PartnerTotalValue { get; init; }
    public double ValueDifference { get; init; }
}

public sealed class TradeParticipantDto
{
    public string UserId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
}

public sealed class TradeMatchDto
{
    public string CardName { get; init; } = string.Empty;
    public string FromUserId { get; init; } = string.Empty;
    public string ToUserId { get; init; } = string.Empty;
    public BinderCardDto OfferingCard { get; init; } = null!;
}
