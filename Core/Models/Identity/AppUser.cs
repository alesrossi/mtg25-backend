using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;

namespace Core.Models.Identity;

public class AppUser : IdentityUser
{
    public required string DisplayName { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    [JsonIgnore]
    public List<League> Leagues { get; set; } = [];
}