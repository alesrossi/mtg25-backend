namespace API.Dtos.Cards;

public class MinimalCardDto
{
    public required string Name { get; set; } 
    public required string ScryfallId { get; set; }
    public required string OracleId { get; set; }
    public string? ImageUrl { get; set; }
    public string? BackImageUrl { get; set; }
}
