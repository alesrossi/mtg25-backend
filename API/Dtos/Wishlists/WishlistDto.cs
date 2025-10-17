using System;

namespace API.Dtos.Wishlists;

public class WishlistDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsPublic { get; set; }
    public string OwnerId { get; set; } = string.Empty;
    public int CardsCount { get; set; }
    public int IndividualCardsCount { get; set; }
    public IReadOnlyList<WishlistCardDto> Cards { get; set; } = Array.Empty<WishlistCardDto>();
}
