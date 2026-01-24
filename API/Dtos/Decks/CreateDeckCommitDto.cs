using System.ComponentModel.DataAnnotations;

namespace API.Dtos.Decks;

public class CreateDeckCommitDto
{
    [Required(ErrorMessage = "Message is required.")]
    [StringLength(200, ErrorMessage = "Message cannot exceed 200 characters.")]
    public required string Message { get; set; }
}
