using Core.Models.Identity;

namespace API.Dtos.Accounts;

public class SettingsForUserDto
{
    public required int Id { get; set; }
    public MarketProvider? MarketProvider { get; set; }
    public ReferencePrice? ReferencePrice { get; set; } 
    public Currency? Currency { get; set; } 
    public string? LanguageUi { get; set; } = "It";
    public string? LanguageCards { get; set; } = "En";
    public bool? EnabledLocation { get; set; } = false;
    public required string AppUserId { get; set; }
    public required UserDto AppUser { get; set; }
}