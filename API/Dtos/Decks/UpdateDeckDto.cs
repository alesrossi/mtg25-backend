using Core.Enums;

namespace API.Dtos.Decks;

public class UpdateDeckDto
{
    public string? Name { get; set; }
    public DeckFormat? Format { get; set; }
    public string? Image { get; set; }
    public bool? IsPublic { get; set; }
    public string? DeckList { get; set; }
}
