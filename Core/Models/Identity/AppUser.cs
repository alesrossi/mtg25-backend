using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;

namespace Core.Models.Identity;

public class AppUser : IdentityUser
{
    public required string DisplayName { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    [JsonIgnore]
    public ICollection<AppUserLeague> UserLeagues { get; set; } = new List<AppUserLeague>();
    [JsonIgnore]
    public Settings? Settings { get; set; }
    [JsonIgnore]
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}