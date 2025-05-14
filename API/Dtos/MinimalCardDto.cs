namespace API.Dtos;

public class MinimalCardDto
{
    public required string Name { get; set; } 
    public required string OracleId { get; set; }
    public string? ImageUrl { get; set; }
}