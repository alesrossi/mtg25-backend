using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace API.Dtos.Trades;

public sealed class UpdateTradeRequest
{
    public IReadOnlyList<TradeMatchUpdateDto> InitiatorMatches { get; init; } = Array.Empty<TradeMatchUpdateDto>();
    public IReadOnlyList<TradeMatchUpdateDto> PartnerMatches { get; init; } = Array.Empty<TradeMatchUpdateDto>();
}

public sealed class TradeMatchUpdateDto
{
    [Required]
    public string MatchId { get; init; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int? QuantityToTrade { get; init; }

    public bool? IsSelected { get; init; }
}
