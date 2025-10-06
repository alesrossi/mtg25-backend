namespace API.Dtos.Decks;

public class DeckDto
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Format { get; set; }
    public int NumberOfCards { get; set; }
    public int NumberOfMainBoardCards { get; set; }
    public int NumberOfSideBoardCards { get; set; }
    public double TotalPrice { get; set; }
    public string? Image { get; set; }
    public required string OwnerId { get; set; }
}