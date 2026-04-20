using System.ComponentModel.DataAnnotations;

namespace API.Dtos.Teams;

public class CreateTeamCollectionViewDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public List<int> CollectionIds { get; set; } = [];
}
