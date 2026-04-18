using Core.Enums;
using Core.Models.Identity;

namespace API.Dtos.Accounts;

public class UpdateSettingsDto
{
    public MarketProvider? MarketProvider { get; set; }
    public ReferencePrice? ReferencePrice { get; set; }
    public Currency? Currency { get; set; }
    public Language? LanguageUi { get; set; }
    public Language? LanguageCards { get; set; }
    public bool? EnabledLocation { get; set; }
    public string? CompanionName { get; set; }
    public string? Avatar { get; set; }
}
