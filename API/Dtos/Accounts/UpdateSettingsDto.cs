using Core.Models.Identity;

namespace API.Dtos.Accounts;

public class UpdateSettingsDto
{
    public MarketProvider? MarketProvider { get; set; }
    public ReferencePrice? ReferencePrice { get; set; }
    public Currency? Currency { get; set; }
    public string? LanguageUi { get; set; }
    public string? LanguageCards { get; set; }
    public bool? EnabledLocation { get; set; }
}
