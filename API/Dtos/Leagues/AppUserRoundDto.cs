namespace API.Dtos.Leagues;

public class AppUserRoundDto
{
    public string UserId { get; set; } = string.Empty;
    public int Position { get; set; }
    public double Score { get; set; }
}
