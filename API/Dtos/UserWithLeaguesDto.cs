namespace API.Dtos;

public class UserWithLeaguesDto
{
    public required string Id { get; set; }
    public required string DisplayName { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public List<LeagueDto> Leagues { get; set; } = [];
}