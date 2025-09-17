using System.ComponentModel.DataAnnotations;

namespace API.Dtos.Decks;

public class DeckImportRequestDto
{
    [Required(ErrorMessage = "Deck name is required.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Deck name must be between 1 and 100 characters.")]
    public required string Name { get; set; }

    [Required(ErrorMessage = "Deck format is required.")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "Deck format must be between 2 and 50 characters.")]
    public required string Format { get; set; }

    [Required(ErrorMessage = "Decklist text is required.")]
    [MinLength(1, ErrorMessage = "Decklist text must not be empty.")]
    public required string Decklist { get; set; }
}
