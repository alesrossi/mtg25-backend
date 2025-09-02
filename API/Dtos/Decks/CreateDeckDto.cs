namespace API.Dtos.Decks;

public class CreateDeckDto
{
    public required string Name { get; set; }
    public required string Format { get; set; }
}