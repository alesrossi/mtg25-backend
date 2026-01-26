using Core.Enums;

namespace Core.Models.Identity;

public class Settings : BaseModel
{
    public MarketProvider? MarketProvider { get; set; }
    public ReferencePrice? ReferencePrice { get; set; } 
    public Currency? Currency { get; set; } 
    public Language LanguageUi { get; set; } = Language.It;
    public Language LanguageCards { get; set; } = Language.En;
    public bool? EnabledLocation { get; set; } = false;
    public required string AppUserId { get; set; }
    public required AppUser AppUser { get; set; }
}

public enum ReferencePrice
{
    Min,
    Avg,
    Max
}

public enum MarketProvider
{
    Mkm,
    Tcg
}

