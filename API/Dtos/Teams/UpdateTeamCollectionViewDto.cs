using System.ComponentModel.DataAnnotations;

namespace API.Dtos.Teams;

public class UpdateTeamCollectionViewDto
{
    [MaxLength(100)]
    public string? Name { get; set; }

    public List<int>? CollectionIds { get; set; }
}
