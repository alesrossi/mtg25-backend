using Core.Models;

namespace API.Dtos.Cards;

public class GroupedCardsDto
{
    public string GroupKey { get; set; } = string.Empty;
    public int Count { get; set; }
    public List<Card> Cards { get; set; } = [];
}

public class GroupedCardsPaginationDto
{
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public int TotalGroups { get; set; }
    public int TotalCards { get; set; }
    public List<GroupedCardsDto> Groups { get; set; } = [];
}