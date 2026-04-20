using System.ComponentModel.DataAnnotations;

namespace API.Dtos.Binders;

public class CreateBinderDto
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    public bool IsPublic { get; set; }
    public int? TeamId { get; set; }
}
