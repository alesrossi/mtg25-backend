namespace API.Dtos.Leagues;

public class AppUserRoundDto
{
    public string UserId { get; set; } = string.Empty;
    public int Position { get; set; }
    public double Score { get; set; }
    public int Omw { get; set; }
    public int Gw { get; set; }
    public int Ogw { get; set; }
}
