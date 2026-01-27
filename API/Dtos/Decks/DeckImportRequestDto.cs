using System.ComponentModel.DataAnnotations;
using Core.Enums;

namespace API.Dtos.Decks;

public class DeckImportRequestDto
{
    [Required(ErrorMessage = "Deck name is required.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Deck name must be between 1 and 100 characters.")]
    public required string Name { get; set; }

    [Required(ErrorMessage = "Deck format is required.")]
    public required DeckFormat Format { get; set; }

    [Required(ErrorMessage = "Decklist text is required.")]
    [MinLength(1, ErrorMessage = "Decklist text must not be empty.")]
    public required string Decklist { get; set; }
}
