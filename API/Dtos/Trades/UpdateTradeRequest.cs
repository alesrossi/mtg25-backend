using System.ComponentModel.DataAnnotations;

namespace API.Dtos.Trades;

public sealed class UpdateTradeRequest
{
    public IReadOnlyList<TradeMatchUpdateDto> InitiatorMatches { get; init; } = [];
    public IReadOnlyList<TradeMatchUpdateDto> PartnerMatches { get; init; } = [];
    
    [Range(1, int.MaxValue)]
    public int? InitiatorCollectionId { get; init; }

    [Range(1, int.MaxValue)]
    public int? PartnerCollectionId { get; init; }
}

public sealed class TradeMatchUpdateDto
{
    [Required]
    public string MatchId { get; init; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int? QuantityToTrade { get; init; }

    public bool? IsSelected { get; init; }
}
