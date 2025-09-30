namespace API.Dtos.Wishlists;

public class WishlistCardDto
{
    public int Id { get; set; }
    public int WishlistId { get; set; }
    public string OracleId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int DesiredQuantity { get; set; }
    public bool IsFoil { get; set; }
    public string? Language { get; set; }
    public string? Notes { get; set; }
}
