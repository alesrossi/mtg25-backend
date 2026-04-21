namespace API.Dtos.Binders;

public class BinderDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsPublic { get; set; }
    public string OwnerId { get; set; } = string.Empty;
    public int CardsCount { get; set; }
    public double TotalPrice { get; set; }
    public IReadOnlyList<BinderCardDto> Cards { get; set; } = [];
    public int? TeamId { get; set; }
    public string? TeamName { get; set; }
}
