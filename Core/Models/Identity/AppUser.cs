using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;

namespace Core.Models.Identity;

public class AppUser : IdentityUser
{
    [MaxLength(100)]
    public required string DisplayName { get; set; }
    [MaxLength(100)]
    public required string FirstName { get; set; }
    [MaxLength(100)]
    public required string LastName { get; set; }
    [MaxLength(100)]
    public string? CompanionName { get; set; }
    public bool IsGoogleAccount { get; set; } = false;
    [JsonIgnore]
    public ICollection<AppUserLeague> UserLeagues { get; set; } = new List<AppUserLeague>();
    [JsonIgnore]
    public Settings? Settings { get; set; }
    [JsonIgnore]
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    [JsonIgnore]
    public ICollection<LeagueRoleAssignment> LeagueRoles { get; set; } = new List<LeagueRoleAssignment>();
    [JsonIgnore]
    public ICollection<AppUserFriend> Friendships { get; set; } = new List<AppUserFriend>();
    [JsonIgnore]
    public ICollection<AppUserFriend> FriendshipsReceived { get; set; } = new List<AppUserFriend>();
}
