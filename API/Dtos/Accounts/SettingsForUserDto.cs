using Core.Enums;
using Core.Models.Identity;

namespace API.Dtos.Accounts;

public class SettingsForUserDto
{
    public required int Id { get; set; }
    public MarketProvider? MarketProvider { get; set; }
    public ReferencePrice? ReferencePrice { get; set; } 
    public Currency? Currency { get; set; } 
    public Language LanguageUi { get; set; } = Language.It;
    public Language LanguageCards { get; set; } = Language.En;
    public bool? EnabledLocation { get; set; } = false;
    public required string AppUserId { get; set; }
    public required UserDto AppUser { get; set; }
}
