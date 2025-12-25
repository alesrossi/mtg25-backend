namespace API.Dtos.Leagues;

public class UserWithScore
{
    public required string UserId { get; set; }
    public int? Wins { get; set; }
    public int? Draws { get; set; }
    public int? Losses { get; set; }
}