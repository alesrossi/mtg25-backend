using Microsoft.AspNetCore.Identity;

namespace Core.Models.Identity;

public class AppUser : IdentityUser
{
    public string DisplayName { get; set; }
    public Settings Settings { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
}