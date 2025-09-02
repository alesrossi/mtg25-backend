using System.ComponentModel.DataAnnotations;

namespace API.Dtos.Collections;

public class NewCollectionDto
{
    [Required(ErrorMessage = "Collection name is required")]
    [StringLength(32, MinimumLength = 1, ErrorMessage = "Collection name must be between 1 and 32 characters")]
    public required string Name { get; set; } 
    public required string Color { get; set; }
}