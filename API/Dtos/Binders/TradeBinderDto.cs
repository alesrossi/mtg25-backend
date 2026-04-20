namespace API.Dtos.Binders;

public class TradeBinderDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public int? TeamId { get; set; }
    public string? TeamName { get; set; }
}
