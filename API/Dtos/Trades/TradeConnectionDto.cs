using System;
using System.Collections.Generic;
using API.Dtos.Wishlists;

namespace API.Dtos.Trades;

public sealed class TradeConnectionDto
{
    public string TradeId { get; init; } = string.Empty;
    public TradeParticipantDto Initiator { get; init; } = new();
    public TradeParticipantDto Partner { get; init; } = new();
}

public sealed class TradeParticipantDto
{
    public string UserId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public IReadOnlyList<WishlistSummaryDto> Wishlists { get; init; } = Array.Empty<WishlistSummaryDto>();
}
