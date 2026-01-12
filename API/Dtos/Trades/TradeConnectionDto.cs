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
    public double InitiatorTotalValue { get; set; }
    public double PartnerTotalValue { get; set; }
    public double ValueDifference { get; set; }
    public int? InitiatorCollectionId { get; set; }
    public int? PartnerCollectionId { get; set; }
    public bool IsLiveTrading { get; set; } = true;
}

public sealed class TradeParticipantDto
{
    public string UserId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
}

public sealed class TradeMatchDto
{
    public string MatchId { get; set; } = string.Empty;
    public string CardName { get; init; } = string.Empty;
    public string FromUserId { get; init; } = string.Empty;
    public string ToUserId { get; init; } = string.Empty;
    public BinderCardDto OfferingCard { get; init; } = null!;
    public bool IsSelected { get; set; } = true;
}
