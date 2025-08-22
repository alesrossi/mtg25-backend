namespace Core.Models.Identity;

public class Settings
{
    public int Id { get; set; }
    public required string MarketProvider { get; set; }
    public required string ReferencePrice { get; set; }
    public required string Currency { get; set; }
    public required string Language { get; set; }
    public required string AppUserId { get; set; }
}