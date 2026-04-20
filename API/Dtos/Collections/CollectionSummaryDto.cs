namespace API.Dtos.Collections;

public class CollectionSummaryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int CardsCount { get; set; }
    public double TotalPrice { get; set; }
    public string? Currency { get; set; }
    public string OwnerDisplayName { get; set; } = string.Empty;
}
