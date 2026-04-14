using Core.Enums;

namespace API.Dtos.Decks;

public class CreateDeckDto
{
    public required string Name { get; set; }
    public required DeckFormat Format { get; set; }
    public required bool IsPublic { get; set; }
}
