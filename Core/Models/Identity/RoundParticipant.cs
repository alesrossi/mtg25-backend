using System.ComponentModel.DataAnnotations;

namespace Core.Models.Identity;

public class RoundParticipant
{
    [MaxLength(100)]
    public required string UserId { get; set; }
    public required AppUser User { get; set; }
    public int RoundId { get; set; }
    public required Round Round { get; set; }
}
