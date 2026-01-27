using System.ComponentModel.DataAnnotations;

namespace Core.Models.Identity;

public class AppUserRound
{
    [MaxLength(100)]
    public required string UserId { get; set; }
    public required AppUser User { get; set; }
    public int RoundId { get; set; }
    public required Round Round { get; set; }
    public int Position { get; set; }
    public double Score { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int Draws { get; set; }
    public int Omw { get; set; }
    public int Gw { get; set; }
    public int Ogw { get; set; }
}