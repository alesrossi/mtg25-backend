namespace API.Dtos.Binders;

public class BinderSummaryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsPublic { get; set; }
    public int CardsCount { get; set; }
}
