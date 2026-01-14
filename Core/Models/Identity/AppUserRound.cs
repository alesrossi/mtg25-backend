namespace Core.Models.Identity;

public class AppUserRound
{
    public string UserId { get; set; }
    public required AppUser User { get; set; }
    public int RoundId { get; set; }
    public required Round Round { get; set; }
    public int Position { get; set; }
    public double Score { get; set; }
}