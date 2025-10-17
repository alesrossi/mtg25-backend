using System.ComponentModel.DataAnnotations;

namespace API.Dtos.Decks;

public class CreateDeckCardDto
{
    [Required(ErrorMessage = "Oracle ID is required.")]
    [StringLength(64, ErrorMessage = "Oracle ID cannot exceed 64 characters.")]
    public required string OracleId { get; set; }

    [Required(ErrorMessage = "Card name is required.")]
    [StringLength(128, ErrorMessage = "Card name cannot exceed 128 characters.")]
    public required string Name { get; set; }

    [Required(ErrorMessage = "Set code is required.")]
    [StringLength(10, MinimumLength = 2, ErrorMessage = "Set code must be between 2 and 10 characters.")]
    public required string SetCode { get; set; }

    public string? SetName { get; set; }

    [Required(ErrorMessage = "Image URL is required.")]
    [StringLength(512, ErrorMessage = "Image URL cannot exceed 512 characters.")]
    public required string ImageUrl { get; set; }
    public required string ArtCrop { get; set; }

    public List<string> ColorIdentity { get; set; } = [];
    public string? Rarity { get; set; }

    public string? CollectorNumber { get; set; }

    [Range(0, 250, ErrorMessage = "Maindeck quantity must be between 0 and 4.")]
    public int MaindeckQuantity { get; set; }

    [Range(0, 250, ErrorMessage = "Sideboard quantity must be between 0 and 4.")]
    public int SideboardQuantity { get; set; }

    public int? OwnedCardId { get; set; }
}
