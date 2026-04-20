using API.Dtos.Collections;

namespace API.Dtos.Teams;

public class TeamCollectionViewDto
{
    public int Id { get; set; }
    public int TeamId { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<int> CollectionIds { get; set; } = [];
    public List<CollectionSummaryDto> Collections { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public string CreatedByDisplayName { get; set; } = string.Empty;
    public int TotalCards { get; set; }
    public double TotalPrice { get; set; }
}
