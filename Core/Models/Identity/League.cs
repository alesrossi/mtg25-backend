namespace Core.Models.Identity;

public class League : BaseModel
{
    public required string Name { get; set; }
    public List<AppUser> Users { get; set; } = [];
}