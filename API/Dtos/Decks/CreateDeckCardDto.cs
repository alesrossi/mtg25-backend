using System.ComponentModel.DataAnnotations;

namespace API.Dtos.Decks;

public class CreateDeckCardDto
{
    [Required(ErrorMessage = "Name is required.")]
    [StringLength(64, ErrorMessage = "Name cannot exceed 64 characters.")]
    public required string Name { get; set; }

    [Range(0, 250, ErrorMessage = "Maindeck quantity must be between 0 and 4.")]
    public int MaindeckQuantity { get; set; }

    [Range(0, 250, ErrorMessage = "Sideboard quantity must be between 0 and 4.")]
    public int SideboardQuantity { get; set; }
}
