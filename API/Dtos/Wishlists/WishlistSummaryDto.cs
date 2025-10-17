namespace API.Dtos.Wishlists;

public class WishlistSummaryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsPublic { get; set; }
    public int CardsCount { get; set; }
    public int IndividualCardsCount { get; set; }
}
