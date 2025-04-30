namespace API.Dtos;

public class CollectionDto
{
    public string Name { get; set; } = null!;
    public string Color { get; set; } = null!;
    public int NumberOfCards { get; set; }
    public double TotalPrice { get; set; }
    public List<InternalCardDto> Cards { get; set; } = new();

}