using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Core.Enums;

namespace Core.Models.Identity;

public class LeagueRoleAssignment
{
    public int Id { get; set; }
    [MaxLength(100)]
    public required string UserId { get; set; }
    [JsonIgnore]
    public AppUser User { get; set; } = null!;
    public int LeagueId { get; set; }
    [JsonIgnore]
    public League League { get; set; } = null!;
    public LeagueRole Roles { get; set; } = LeagueRole.Player;
}
