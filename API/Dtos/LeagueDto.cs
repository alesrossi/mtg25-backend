namespace API.Dtos;

public class LeagueDto
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Code { get; set; }
    public required string Format { get; set; }
    public DateTime? EndDate { get; set; }
    public required int Score { get; set; }
}